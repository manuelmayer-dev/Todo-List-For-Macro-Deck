namespace TodoList.Core;

public enum TodoSortOrder
{
	DueDate,
	CreatedDate,
	Manual,
}

public enum TodoStatusFilter
{
	All,
	Due,
	Overdue,
	DueOrOverdue,
}

public static class TodoOrdering
{
	/// <summary>
	/// Sorts open todos before completed ones. Open todos follow <paramref name="order"/>, optionally with
	/// overdue todos first; completed todos are listed most recently completed first.
	/// </summary>
	public static IReadOnlyList<TodoItem> Sort(
		IEnumerable<TodoItem> todos,
		TodoSortOrder order,
		bool overdueFirst,
		DateTimeOffset now,
		TodoThresholds thresholds)
	{
		ArgumentNullException.ThrowIfNull(todos);

		var items = todos.ToList();
		var open = items.Where(t => !t.IsCompleted);
		var ordered = overdueFirst
			? open.OrderByDescending(t => TodoStatusRules.Evaluate(t, now, thresholds) == TodoStatus.Overdue)
			: open.OrderBy(_ => 0);

		ordered = order switch
		{
			TodoSortOrder.DueDate => ordered
				.ThenBy(t => t.DueAt is null)
				.ThenBy(t => t.DueAt)
				.ThenBy(t => t.SortOrder),
			TodoSortOrder.CreatedDate => ordered
				.ThenByDescending(t => t.CreatedAt)
				.ThenBy(t => t.SortOrder),
			TodoSortOrder.Manual => ordered
				.ThenBy(t => t.SortOrder)
				.ThenBy(t => t.CreatedAt),
			_ => throw new ArgumentOutOfRangeException(nameof(order), order, null),
		};

		var completed = items.Where(t => t.IsCompleted).OrderByDescending(t => t.CompletedAt);
		return [.. ordered, .. completed];
	}

	public static bool Matches(this TodoStatusFilter filter, TodoStatus status) => filter switch
	{
		TodoStatusFilter.All => true,
		TodoStatusFilter.Due => status == TodoStatus.Due,
		TodoStatusFilter.Overdue => status == TodoStatus.Overdue,
		TodoStatusFilter.DueOrOverdue => status is TodoStatus.Due or TodoStatus.Overdue,
		_ => throw new ArgumentOutOfRangeException(nameof(filter), filter, null),
	};
}
