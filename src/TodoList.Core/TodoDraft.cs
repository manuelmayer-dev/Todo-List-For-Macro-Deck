namespace TodoList.Core;

/// <summary>
/// The user-editable fields of a todo, as entered. <see cref="TodoValidation"/> normalizes it.
/// </summary>
public sealed record TodoDraft(string Title, string? Description = null, DateTimeOffset? DueAt = null);
