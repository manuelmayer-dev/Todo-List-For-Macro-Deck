using Serilog;
using TodoList.Core.Storage;

namespace TodoList.Core;

/// <summary>
/// The one owner of todo state in the process. The Macro Deck integration, the widget, the modal and the
/// desktop UI all go through it, so they always see the same data. Writes are serialized and persisted
/// before the in-memory snapshot is replaced; <see cref="Changed"/> fires after the commit.
/// </summary>
public sealed class TodoService : IDisposable
{
	private readonly TodoDatabase _database;
	private readonly TimeProvider _clock;
	private readonly ILogger _logger;
	private readonly SemaphoreSlim _writeGate = new(1, 1);
	private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private volatile TodoSnapshot _snapshot = TodoSnapshot.Empty;

	public TodoService(TodoDatabase database, TimeProvider clock, ILogger logger)
	{
		_database = database;
		_clock = clock;
		_logger = logger.ForContext<TodoService>();
	}

	public event EventHandler<TodoChangedEventArgs>? Changed;

	public TodoSnapshot Snapshot => _snapshot;

	/// <summary>Completes once the database is open and loaded, or faults if that failed.</summary>
	public Task Ready => _ready.Task;

	public async Task LoadAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _database.InitializeAsync(cancellationToken).ConfigureAwait(false);
			var items = await _database.LoadTodosAsync(cancellationToken).ConfigureAwait(false);
			_snapshot = new TodoSnapshot(items);
			_logger.Information("Loaded {Count} todos from {Path}", items.Count, _database.FilePath);
			_ready.TrySetResult();
		}
		catch (Exception exception)
		{
			_ready.TrySetException(exception);
			throw;
		}
	}

	public Task<TodoItem> CreateAsync(TodoDraft draft, CancellationToken cancellationToken)
	{
		var normalized = TodoValidation.Normalize(draft);
		return WriteAsync(snapshot =>
		{
			var todo = new TodoItem
			{
				Id = Guid.NewGuid(),
				Title = normalized.Title,
				Description = normalized.Description,
				CreatedAt = Now(),
				DueAt = Truncate(normalized.DueAt),
				SortOrder = snapshot.Items.Count == 0 ? 0 : snapshot.Items.Max(t => t.SortOrder) + 1,
			};
			return Plan(TodoChangeKind.Created, todo, todo);
		}, cancellationToken)!;
	}

	/// <summary>Replaces title, description and due date. Returns null when the todo does not exist.</summary>
	public Task<TodoItem?> UpdateAsync(Guid id, TodoDraft draft, CancellationToken cancellationToken)
	{
		var normalized = TodoValidation.Normalize(draft);
		return WriteAsync(snapshot =>
		{
			if (snapshot.Find(id) is not { } existing)
			{
				return null;
			}

			var dueAt = Truncate(normalized.DueAt);
			var updated = existing with
			{
				Title = normalized.Title,
				Description = normalized.Description,
				DueAt = dueAt,
			};

			// A new deadline starts a new Due and Overdue cycle.
			if (dueAt != existing.DueAt)
			{
				updated = updated with { DueNotifiedAt = null, OverdueNotifiedAt = null };
			}

			return updated == existing ? NoChange(existing) : Plan(TodoChangeKind.Updated, updated, updated);
		}, cancellationToken);
	}

	/// <summary>Completes the todo. Completing a completed todo changes nothing. Null when it does not exist.</summary>
	public Task<TodoItem?> CompleteAsync(Guid id, CancellationToken cancellationToken) =>
		WriteAsync(snapshot => snapshot.Find(id) switch
		{
			null => null,
			{ IsCompleted: true } done => NoChange(done),
			var open => Plan(TodoChangeKind.Completed, open with { CompletedAt = Now() }),
		}, cancellationToken);

	/// <summary>Reopens the todo and clears its transition marks, so Due and Overdue are evaluated again.</summary>
	public Task<TodoItem?> ReopenAsync(Guid id, CancellationToken cancellationToken) =>
		WriteAsync(snapshot => snapshot.Find(id) switch
		{
			null => null,
			{ IsCompleted: false } open => NoChange(open),
			var done => Plan(
				TodoChangeKind.Reopened,
				done with { CompletedAt = null, DueNotifiedAt = null, OverdueNotifiedAt = null }),
		}, cancellationToken);

	/// <summary>Deletes the todo and returns it as it was, or null when it does not exist.</summary>
	public Task<TodoItem?> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
		WriteAsync(snapshot => snapshot.Find(id) is { } existing
			? new WritePlan(TodoChangeKind.Deleted, existing, [], [id])
			: null, cancellationToken);

	/// <summary>
	/// Moves an open todo to <paramref name="index"/> in the manual order of open todos. Returns false when it
	/// does not exist or is completed.
	/// </summary>
	public async Task<bool> MoveAsync(Guid id, int index, CancellationToken cancellationToken)
	{
		var result = await WriteAsync(snapshot =>
		{
			var open = snapshot.Open.OrderBy(t => t.SortOrder).ThenBy(t => t.CreatedAt).ToList();
			var moving = open.Find(t => t.Id == id);
			if (moving is null)
			{
				return null;
			}

			open.Remove(moving);
			open.Insert(Math.Clamp(index, 0, open.Count), moving);
			var renumbered = open
				.Select((t, i) => t with { SortOrder = i })
				.Where(t => snapshot.Find(t.Id)!.SortOrder != t.SortOrder)
				.ToList();
			return renumbered.Count == 0
				? NoChange(moving)
				: new WritePlan(TodoChangeKind.Reordered, moving, renumbered, []);
		}, cancellationToken).ConfigureAwait(false);
		return result is not null;
	}

	/// <summary>
	/// Persists transition marks for the given todos if they are still open and still unmarked, and returns
	/// the transitions that were actually recorded. Used by <see cref="Scheduling.DueMonitor"/>.
	/// </summary>
	internal async Task<IReadOnlyList<Scheduling.DueTransition>> MarkTransitionsAsync(
		IReadOnlyList<Scheduling.DueTransition> candidates,
		DateTimeOffset now,
		CancellationToken cancellationToken)
	{
		var recorded = new List<Scheduling.DueTransition>();
		await WriteAsync(snapshot =>
		{
			var updates = new List<TodoItem>();
			foreach (var candidate in candidates)
			{
				if (snapshot.Find(candidate.Todo.Id) is not { IsCompleted: false } current || current.DueAt != candidate.Todo.DueAt)
				{
					continue;
				}

				var marked = candidate.Kind switch
				{
					Scheduling.DueTransitionKind.Due when current.DueNotifiedAt is null =>
						current with { DueNotifiedAt = now },
					// Reaching Overdue also settles Due, so a skipped Due is never emitted late.
					Scheduling.DueTransitionKind.Overdue when current.OverdueNotifiedAt is null =>
						current with { DueNotifiedAt = current.DueNotifiedAt ?? now, OverdueNotifiedAt = now },
					_ => null,
				};

				if (marked is not null)
				{
					updates.Add(marked);
					recorded.Add(candidate with { Todo = marked });
				}
			}

			return updates.Count == 0 ? null : new WritePlan(TodoChangeKind.TransitionsMarked, null, updates, []);
		}, cancellationToken).ConfigureAwait(false);
		return recorded;
	}

	/// <summary>
	/// Moves the store to <paramref name="databasePath"/> (see <see cref="TodoDatabase.SwitchToAsync"/>) and
	/// reloads, so every surface shows what the new file holds.
	/// </summary>
	internal async Task RelocateAsync(string databasePath, bool adoptExisting, CancellationToken cancellationToken)
	{
		await Ready.WaitAsync(cancellationToken).ConfigureAwait(false);

		TodoChangedEventArgs change;
		await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			await _database.SwitchToAsync(databasePath, adoptExisting, cancellationToken).ConfigureAwait(false);
			var next = new TodoSnapshot(await _database.LoadTodosAsync(cancellationToken).ConfigureAwait(false));
			_snapshot = next;
			change = new TodoChangedEventArgs(TodoChangeKind.Reloaded, null, next);
			_logger.Information("Loaded {Count} todos from {Path}", next.Items.Count, _database.FilePath);
		}
		finally
		{
			_writeGate.Release();
		}

		RaiseChanged(change);
	}

	public void Dispose() => _writeGate.Dispose();

	private async Task<TodoItem?> WriteAsync(Func<TodoSnapshot, WritePlan?> plan, CancellationToken cancellationToken)
	{
		await Ready.WaitAsync(cancellationToken).ConfigureAwait(false);

		TodoChangedEventArgs change;
		await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var current = _snapshot;
			var write = plan(current);
			if (write is null)
			{
				return null;
			}

			if (write.Upserts.Count == 0 && write.Deletions.Count == 0)
			{
				return write.Result;
			}

			await _database.SaveAsync(write.Upserts, write.Deletions, cancellationToken).ConfigureAwait(false);

			var byId = current.Items.ToDictionary(t => t.Id);
			foreach (var id in write.Deletions)
			{
				byId.Remove(id);
			}

			foreach (var todo in write.Upserts)
			{
				byId[todo.Id] = todo;
			}

			var next = new TodoSnapshot(byId.Values);
			_snapshot = next;
			change = new TodoChangedEventArgs(write.Kind, write.Result, next);
		}
		finally
		{
			_writeGate.Release();
		}

		RaiseChanged(change);
		return change.Todo;
	}

	private void RaiseChanged(TodoChangedEventArgs change)
	{
		if (Changed is not { } handlers)
		{
			return;
		}

		// One failing subscriber (a closed window, a dropped session) must not hide the change from the others.
		foreach (EventHandler<TodoChangedEventArgs> handler in handlers.GetInvocationList())
		{
			try
			{
				handler(this, change);
			}
			catch (Exception exception)
			{
				_logger.Error(exception, "A todo change subscriber failed for {Kind}", change.Kind);
			}
		}
	}

	private DateTimeOffset Now() => Truncate(_clock.GetUtcNow())!.Value;

	// The database keeps milliseconds, so in-memory values are truncated the same way to compare equal.
	private static DateTimeOffset? Truncate(DateTimeOffset? value) =>
		value is { } v ? DateTimeOffset.FromUnixTimeMilliseconds(v.ToUniversalTime().ToUnixTimeMilliseconds()) : null;

	private static WritePlan Plan(TodoChangeKind kind, TodoItem todo, TodoItem? result = null) =>
		new(kind, result ?? todo, [todo], []);

	private static WritePlan NoChange(TodoItem todo) => new(TodoChangeKind.Updated, todo, [], []);

	private sealed record WritePlan(
		TodoChangeKind Kind,
		TodoItem? Result,
		IReadOnlyCollection<TodoItem> Upserts,
		IReadOnlyCollection<Guid> Deletions);
}
