namespace TodoList.Core;

public enum TodoValidationError
{
	TitleRequired,
	TitleTooLong,
	DescriptionTooLong,
}

public sealed class TodoValidationException(TodoValidationError error)
	: Exception($"Todo validation failed: {error}.")
{
	public TodoValidationError Error { get; } = error;
}

public static class TodoValidation
{
	public const int MaxTitleLength = 200;

	public const int MaxDescriptionLength = 2000;

	/// <summary>
	/// Trims the draft, turns a blank description into none and normalizes the due date to UTC.
	/// </summary>
	public static TodoDraft Normalize(TodoDraft draft)
	{
		ArgumentNullException.ThrowIfNull(draft);

		var title = (draft.Title ?? string.Empty).Trim();
		if (title.Length == 0)
		{
			throw new TodoValidationException(TodoValidationError.TitleRequired);
		}

		if (title.Length > MaxTitleLength)
		{
			throw new TodoValidationException(TodoValidationError.TitleTooLong);
		}

		var description = string.IsNullOrWhiteSpace(draft.Description) ? null : draft.Description.Trim();
		if (description is { Length: > MaxDescriptionLength })
		{
			throw new TodoValidationException(TodoValidationError.DescriptionTooLong);
		}

		return new TodoDraft(title, description, draft.DueAt?.ToUniversalTime());
	}
}
