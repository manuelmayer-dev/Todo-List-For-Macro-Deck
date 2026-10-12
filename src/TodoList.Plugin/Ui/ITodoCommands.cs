using MacroDeck.Localization;
using TodoList.Core;

namespace TodoList.Plugin.Ui;

/// <summary>
/// The writes Macro Deck views perform. A failure comes back as a localized message instead of an exception.
/// </summary>
public interface ITodoCommands
{
	Task<LocalizedText?> CreateAsync(TodoDraft draft, CancellationToken cancellationToken);

	Task<LocalizedText?> UpdateAsync(Guid id, TodoDraft draft, CancellationToken cancellationToken);

	Task<LocalizedText?> SetCompletedAsync(Guid id, bool completed, CancellationToken cancellationToken);

	Task<LocalizedText?> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
