namespace TodoList.Core;

/// <summary>
/// The derived state of a todo. The states are mutually exclusive; see <see cref="TodoStatusRules"/>.
/// </summary>
public enum TodoStatus
{
	Open,
	Due,
	Overdue,
	Completed,
}

/// <summary>
/// The two configurable thresholds around a deadline.
/// </summary>
/// <param name="DueSoon">How long before the deadline a todo becomes Due.</param>
/// <param name="OverdueGrace">How long after the deadline a todo becomes Overdue.</param>
public sealed record TodoThresholds(TimeSpan DueSoon, TimeSpan OverdueGrace)
{
	public static TodoThresholds Default { get; } = new(TimeSpan.FromMinutes(60), TimeSpan.Zero);
}

/// <summary>
/// The single definition of Open, Due and Overdue used by the widget, modal, desktop UI, variables and events.
/// A todo with deadline D is Due from D - DueSoon (inclusive) until D + OverdueGrace (exclusive), and
/// Overdue from D + OverdueGrace (inclusive) on. With both thresholds at zero it goes straight to Overdue.
/// </summary>
public static class TodoStatusRules
{
	public static TodoStatus Evaluate(TodoItem todo, DateTimeOffset now, TodoThresholds thresholds)
	{
		ArgumentNullException.ThrowIfNull(todo);
		ArgumentNullException.ThrowIfNull(thresholds);

		if (todo.IsCompleted)
		{
			return TodoStatus.Completed;
		}

		if (todo.DueAt is not { } due)
		{
			return TodoStatus.Open;
		}

		if (now >= OverdueAt(due, thresholds))
		{
			return TodoStatus.Overdue;
		}

		return now >= DueStartsAt(due, thresholds) ? TodoStatus.Due : TodoStatus.Open;
	}

	public static DateTimeOffset DueStartsAt(DateTimeOffset dueAt, TodoThresholds thresholds) =>
		dueAt - thresholds.DueSoon;

	public static DateTimeOffset OverdueAt(DateTimeOffset dueAt, TodoThresholds thresholds) =>
		dueAt + thresholds.OverdueGrace;

	public static string ToInvariantName(this TodoStatus status) => status switch
	{
		TodoStatus.Open => "open",
		TodoStatus.Due => "due",
		TodoStatus.Overdue => "overdue",
		TodoStatus.Completed => "completed",
		_ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
	};
}
