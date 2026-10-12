namespace TodoList.Core;

public sealed record TodoCounts(int Open, int Completed, int Due, int Overdue, int Total);

/// <summary>
/// An immutable view of all todos at one point in time. Safe to read from any thread.
/// </summary>
public sealed class TodoSnapshot
{
	private readonly Dictionary<Guid, TodoItem> _byId;

	public TodoSnapshot(IEnumerable<TodoItem> items)
	{
		ArgumentNullException.ThrowIfNull(items);
		Items = [.. items];
		_byId = Items.ToDictionary(t => t.Id);
	}

	public static TodoSnapshot Empty { get; } = new([]);

	public IReadOnlyList<TodoItem> Items { get; }

	public IEnumerable<TodoItem> Open => Items.Where(t => !t.IsCompleted);

	public IEnumerable<TodoItem> Completed => Items.Where(t => t.IsCompleted);

	public TodoItem? Find(Guid id) => _byId.GetValueOrDefault(id);

	public TodoCounts Count(DateTimeOffset now, TodoThresholds thresholds)
	{
		int open = 0, completed = 0, due = 0, overdue = 0;
		foreach (var todo in Items)
		{
			switch (TodoStatusRules.Evaluate(todo, now, thresholds))
			{
				case TodoStatus.Completed:
					completed++;
					continue;
				case TodoStatus.Due:
					due++;
					break;
				case TodoStatus.Overdue:
					overdue++;
					break;
			}

			open++;
		}

		return new TodoCounts(open, completed, due, overdue, Items.Count);
	}

	/// <summary>
	/// The open todo with the earliest deadline, overdue ones included, or null when no open todo has one.
	/// </summary>
	public TodoItem? NextDue() => Open
		.Where(t => t.DueAt is not null)
		.OrderBy(t => t.DueAt)
		.ThenBy(t => t.SortOrder)
		.FirstOrDefault();

	/// <summary>
	/// The todo to act on next: the earliest deadline, or without any deadline the first open todo in
	/// <paramref name="fallbackOrder"/>. Null when nothing is open.
	/// </summary>
	public TodoItem? Next(TodoSortOrder fallbackOrder, DateTimeOffset now, TodoThresholds thresholds) =>
		NextDue() ?? (Query(fallbackOrder, overdueFirst: false, includeCompleted: false, TodoStatusFilter.All, now, thresholds) is [var first, ..]
			? first
			: null);

	public IReadOnlyList<TodoItem> Query(
		TodoSortOrder order,
		bool overdueFirst,
		bool includeCompleted,
		TodoStatusFilter filter,
		DateTimeOffset now,
		TodoThresholds thresholds)
	{
		var candidates = Items.Where(t =>
		{
			var status = TodoStatusRules.Evaluate(t, now, thresholds);
			return status == TodoStatus.Completed ? includeCompleted && filter == TodoStatusFilter.All : filter.Matches(status);
		});
		return TodoOrdering.Sort(candidates, order, overdueFirst, now, thresholds);
	}
}
