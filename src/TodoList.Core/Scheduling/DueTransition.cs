namespace TodoList.Core.Scheduling;

public enum DueTransitionKind
{
	Due,
	Overdue,
}

public sealed record DueTransition(DueTransitionKind Kind, TodoItem Todo);
