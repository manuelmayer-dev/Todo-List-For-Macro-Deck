namespace TodoList.Core;

public enum TodoChangeKind
{
	Created,
	Updated,
	Completed,
	Reopened,
	Deleted,
	Reordered,

	/// <summary>Only transition marks changed; nothing the user edited.</summary>
	TransitionsMarked,

	/// <summary>The store moved to another file and every todo may have changed.</summary>
	Reloaded,
}

/// <summary>
/// Raised after a change was committed. <see cref="Todo"/> is the todo after the change (before it, for a
/// deletion; the moved todo, for a reorder) and is null for <see cref="TodoChangeKind.TransitionsMarked"/> and <see cref="TodoChangeKind.Reloaded"/>.
/// </summary>
public sealed class TodoChangedEventArgs(TodoChangeKind kind, TodoItem? todo, TodoSnapshot snapshot) : EventArgs
{
	public TodoChangeKind Kind { get; } = kind;

	public TodoItem? Todo { get; } = todo;

	public TodoSnapshot Snapshot { get; } = snapshot;
}
