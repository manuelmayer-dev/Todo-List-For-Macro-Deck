using Serilog;
using TodoList.Core.Settings;

namespace TodoList.Core.Scheduling;

/// <summary>
/// Detects Due and Overdue transitions and raises each one exactly once per deadline. It arms one timer for
/// the next transition instead of polling, persists a mark before raising, and on start reconciles what was
/// missed (only the latest state per todo: Overdue supersedes Due).
/// </summary>
public sealed class DueMonitor : IDisposable
{
	// Long sleeps and wall-clock changes delay a monotonic timer; this bounds how late a transition can be.
	internal static readonly TimeSpan MaxTimerDelay = TimeSpan.FromMinutes(5);

	private static readonly TimeSpan RetryDelay = TimeSpan.FromMinutes(1);

	private readonly TodoService _todos;
	private readonly SettingsService _settings;
	private readonly TimeProvider _clock;
	private readonly ILogger _logger;
	private readonly ITransitionDeliveryGate? _delivery;
	private readonly SemaphoreSlim _reconcileGate = new(1, 1);
	private readonly Lock _stateLock = new();
	private ITimer? _timer;
	private CancellationTokenSource? _running;

	public DueMonitor(TodoService todos, SettingsService settings, TimeProvider clock, ILogger logger, ITransitionDeliveryGate? delivery = null)
	{
		_todos = todos;
		_settings = settings;
		_clock = clock;
		_logger = logger.ForContext<DueMonitor>();
		_delivery = delivery;
	}

	public event EventHandler<DueTransition>? TransitionOccurred;

	public bool IsRunning
	{
		get
		{
			lock (_stateLock)
			{
				return _running is not null;
			}
		}
	}

	/// <summary>The time the timer is armed for, or null when nothing is pending. For diagnostics and tests.</summary>
	public DateTimeOffset? NextCheckAt { get; private set; }

	/// <summary>Starts monitoring and reconciles immediately. Calling it while running does nothing.</summary>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		lock (_stateLock)
		{
			if (_running is not null)
			{
				return Task.CompletedTask;
			}

			_running = new CancellationTokenSource();
			_todos.Changed += OnTodosChanged;
			_settings.Changed += OnSettingsChanged;
		}

		return ReconcileAsync(cancellationToken);
	}

	public void Stop()
	{
		CancellationTokenSource? running;
		lock (_stateLock)
		{
			running = _running;
			_running = null;
			_todos.Changed -= OnTodosChanged;
			_settings.Changed -= OnSettingsChanged;
			_timer?.Dispose();
			_timer = null;
			NextCheckAt = null;
		}

		// Cancelled but not disposed: a reconcile that already read the token may still link to it, and
		// disposing here would turn a clean cancellation into an ObjectDisposedException.
		running?.Cancel();
	}

	public void Dispose()
	{
		Stop();
		_reconcileGate.Dispose();
	}

	/// <summary>Raises every transition that is due now and re-arms the timer.</summary>
	public async Task ReconcileAsync(CancellationToken cancellationToken)
	{
		CancellationToken stopping;
		lock (_stateLock)
		{
			if (_running is null)
			{
				return;
			}

			stopping = _running.Token;
		}

		using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stopping);
		await _reconcileGate.WaitAsync(linked.Token).ConfigureAwait(false);
		try
		{
			// Picks up time zone changes for everything that formats local times later.
			TimeZoneInfo.ClearCachedData();

			var now = _clock.GetUtcNow();
			var thresholds = _settings.Current.Thresholds;
			var candidates = FindPendingTransitions(_todos.Snapshot, now, thresholds);
			if (candidates.Count > 0 && _delivery is { IsOpen: false })
			{
				// Marking now would lose the transition; the owner calls ReconcileAsync once delivery is back.
				_logger.Debug("Holding {Count} due transitions until they can be delivered", candidates.Count);
				Schedule(now, RetryDelay);
				return;
			}

			if (candidates.Count > 0)
			{
				IReadOnlyList<DueTransition> recorded;
				try
				{
					recorded = await _todos.MarkTransitionsAsync(candidates, now, linked.Token).ConfigureAwait(false);
				}
				catch (Storage.TodoStoreException exception)
				{
					// Nothing was marked, so nothing is raised; try again shortly instead of losing the transition.
					_logger.Warning(exception, "Could not record due transitions; retrying in {Delay}", RetryDelay);
					Schedule(now, RetryDelay);
					return;
				}

				foreach (var transition in recorded)
				{
					Raise(transition);
				}
			}

			var next = FindNextTransitionTime(_todos.Snapshot, now, thresholds);

			// Marking takes time, so the delay is measured from the clock as it is now, not from the start.
			var armedAt = _clock.GetUtcNow();
			Schedule(armedAt, next is null ? null : next.Value - armedAt);
		}
		finally
		{
			_reconcileGate.Release();
		}
	}

	internal static IReadOnlyList<DueTransition> FindPendingTransitions(
		TodoSnapshot snapshot,
		DateTimeOffset now,
		TodoThresholds thresholds)
	{
		var pending = new List<DueTransition>();
		foreach (var todo in snapshot.Open)
		{
			switch (TodoStatusRules.Evaluate(todo, now, thresholds))
			{
				case TodoStatus.Overdue when todo.OverdueNotifiedAt is null:
					pending.Add(new DueTransition(DueTransitionKind.Overdue, todo));
					break;
				case TodoStatus.Due when todo.DueNotifiedAt is null:
					pending.Add(new DueTransition(DueTransitionKind.Due, todo));
					break;
			}
		}

		return pending;
	}

	internal static DateTimeOffset? FindNextTransitionTime(
		TodoSnapshot snapshot,
		DateTimeOffset now,
		TodoThresholds thresholds)
	{
		DateTimeOffset? next = null;
		foreach (var todo in snapshot.Open)
		{
			if (todo.DueAt is not { } due)
			{
				continue;
			}

			if (todo.DueNotifiedAt is null)
			{
				next = Earliest(next, TodoStatusRules.DueStartsAt(due, thresholds), now);
			}

			if (todo.OverdueNotifiedAt is null)
			{
				next = Earliest(next, TodoStatusRules.OverdueAt(due, thresholds), now);
			}
		}

		return next;
	}

	private static DateTimeOffset? Earliest(DateTimeOffset? current, DateTimeOffset candidate, DateTimeOffset now) =>
		candidate <= now ? current : current is null || candidate < current ? candidate : current;

	private void Schedule(DateTimeOffset now, TimeSpan? delayUntilNext)
	{
		lock (_stateLock)
		{
			_timer?.Dispose();
			_timer = null;
			NextCheckAt = null;
			if (_running is null || delayUntilNext is not { } delay)
			{
				return;
			}

			if (delay < TimeSpan.Zero)
			{
				delay = TimeSpan.Zero;
			}

			if (delay > MaxTimerDelay)
			{
				delay = MaxTimerDelay;
			}

			_timer = _clock.CreateTimer(_ => RunInBackground("timer"), null, delay, Timeout.InfiniteTimeSpan);
			NextCheckAt = now + delay;
		}
	}

	private void Raise(DueTransition transition)
	{
		_logger.Information("Todo {TodoId} became {Kind}", transition.Todo.Id, transition.Kind);
		if (TransitionOccurred is not { } handlers)
		{
			return;
		}

		foreach (EventHandler<DueTransition> handler in handlers.GetInvocationList())
		{
			try
			{
				handler(this, transition);
			}
			catch (Exception exception)
			{
				_logger.Error(exception, "A due transition subscriber failed");
			}
		}
	}

	private void OnTodosChanged(object? sender, TodoChangedEventArgs change)
	{
		// Marking is the monitor's own write; reconciling again would only re-arm the same timer.
		if (change.Kind != TodoChangeKind.TransitionsMarked)
		{
			RunInBackground("todo change");
		}
	}

	private void OnSettingsChanged(object? sender, TodoSettings settings) => RunInBackground("settings change");

	private void RunInBackground(string trigger) => _ = Task.Run(async () =>
	{
		try
		{
			await ReconcileAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
		}
		catch (Exception exception)
		{
			_logger.Error(exception, "Checking due todos after {Trigger} failed", trigger);
		}
	});
}
