using Serilog;
using TodoList.Core;
using TodoList.Core.Scheduling;
using TodoList.Core.Settings;
using TodoList.Plugin.Hosting;

namespace TodoList.Plugin.Ui;

/// <summary>
/// Keeps <see cref="TodoUiState"/> in sync with the store, the settings and the clock. The minute clock only
/// runs while at least one view is open.
/// </summary>
public sealed class TodoUiBinder : IDisposable
{
	private readonly TodoService _todos;
	private readonly SettingsService _settings;
	private readonly MinuteClock _clock;
	private readonly ILogger _logger;
	private readonly Lock _lock = new();
	private int _openViews;

	public TodoUiBinder(TodoService todos, SettingsService settings, MinuteClock clock, TodoRuntime runtime, ILogger logger)
	{
		_todos = todos;
		_settings = settings;
		_clock = clock;
		_logger = logger.ForContext<TodoUiBinder>();
		State = new TodoUiState(todos.Snapshot, settings.Current, clock.Now, TodoStoreStatus.Loading);
		_todos.Changed += OnTodosChanged;
		_settings.Changed += OnSettingsChanged;
		_ = runtime.Ready.ContinueWith(
			ready => OnReady(ready.Result),
			CancellationToken.None,
			TaskContinuationOptions.OnlyOnRanToCompletion,
			TaskScheduler.Default);
	}

	public TodoUiState State { get; }

	/// <summary>Call when a view opens; dispose the result when it closes.</summary>
	public IDisposable TrackView()
	{
		lock (_lock)
		{
			if (_openViews++ == 0)
			{
				State.Now.Set(_clock.Now);
				_clock.Tick += OnTick;
			}
		}

		return new ViewLease(this);
	}

	public void Dispose()
	{
		_todos.Changed -= OnTodosChanged;
		_settings.Changed -= OnSettingsChanged;
		_clock.Tick -= OnTick;
	}

	private void ReleaseView()
	{
		lock (_lock)
		{
			if (--_openViews == 0)
			{
				_clock.Tick -= OnTick;
			}
		}
	}

	private void OnReady(bool ready)
	{
		State.Snapshot.Set(_todos.Snapshot);
		State.Settings.Set(_settings.Current);
		State.Status.Set(ready ? TodoStoreStatus.Ready : TodoStoreStatus.Failed);
		_logger.Debug("Macro Deck views bound to the store (ready: {Ready})", ready);
	}

	private void OnTodosChanged(object? sender, TodoChangedEventArgs change) => State.Snapshot.Set(change.Snapshot);

	private void OnSettingsChanged(object? sender, TodoSettings settings) => State.Settings.Set(settings);

	private void OnTick(object? sender, DateTimeOffset now) => State.Now.Set(now);

	private sealed class ViewLease(TodoUiBinder binder) : IDisposable
	{
		private int _disposed;

		public void Dispose()
		{
			if (Interlocked.Exchange(ref _disposed, 1) == 0)
			{
				binder.ReleaseView();
			}
		}
	}
}
