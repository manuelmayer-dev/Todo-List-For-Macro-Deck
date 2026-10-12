namespace TodoList.Core;

/// <summary>
/// A persisted todo. Every timestamp is UTC; convert to local time only for display.
/// </summary>
public sealed record TodoItem
{
	public required Guid Id { get; init; }

	public required string Title { get; init; }

	public string? Description { get; init; }

	public required DateTimeOffset CreatedAt { get; init; }

	public DateTimeOffset? DueAt { get; init; }

	public DateTimeOffset? CompletedAt { get; init; }

	public long SortOrder { get; init; }

	/// <summary>When the Due transition was emitted, so it is never emitted twice.</summary>
	public DateTimeOffset? DueNotifiedAt { get; init; }

	/// <summary>When the Overdue transition was emitted, so it is never emitted twice.</summary>
	public DateTimeOffset? OverdueNotifiedAt { get; init; }

	public bool IsCompleted => CompletedAt is not null;
}
