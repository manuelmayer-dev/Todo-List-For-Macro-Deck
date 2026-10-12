using MacroDeck.Localization;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Previews;
using MacroDeck.Ui.Runtime;
using TodoList.Core;
using TodoList.Core.Settings;

namespace TodoList.Plugin.Ui;

/// <summary>
/// Developer previews of the widget, rendered with <c>macrodeck-plugin preview render</c>. They use sample
/// data and never touch the store.
/// </summary>
internal static class TodoWidgetPreviews
{
	[UiPreview("Todos with an overdue item", Profile = UiPreviewProfiles.Widget)]
	public static UiElement Overdue() => Widget(TodoSamples.State());

	[UiPreview("Nothing overdue", Profile = UiPreviewProfiles.Widget)]
	public static UiElement OnTrack() => Widget(TodoSamples.State(withOverdue: false));

	[UiPreview("Many todos, no due dates", Profile = UiPreviewProfiles.Widget)]
	public static UiElement Compact() => Widget(TodoSamples.State(count: 9), TodoWidgetSettings.Default with { ShowDueDates = false, MaxVisible = 6 });

	[UiPreview("Long title", Profile = UiPreviewProfiles.Widget)]
	public static UiElement LongTitle() =>
		Widget(TodoSamples.State(longTitle: "Prepare the slides, the budget overview and the hiring plan for the quarterly review meeting"));

	[UiPreview("Without complete buttons", Profile = UiPreviewProfiles.Widget)]
	public static UiElement ReadOnly() => Widget(TodoSamples.State(), TodoWidgetSettings.Default with { Interactive = false });

	// The preview renderer measures cells against the tile's shorter side, so a 2x2 tile never reaches the
	// large layout there; these render it directly. Decks count spans and pick it on their own.
	[UiPreview("Large layout", Profile = UiPreviewProfiles.Widget)]
	public static UiElement Large() =>
		TodoWidgetView.Large(TodoSamples.State(count: 7), TodoWidgetSettings.Default, PreviewCommands.Instance, new UiState<int>(0));

	[UiPreview("Large layout without complete buttons", Profile = UiPreviewProfiles.Widget)]
	public static UiElement LargeReadOnly() =>
		TodoWidgetView.Large(TodoSamples.State(count: 7), TodoWidgetSettings.Default with { Interactive = false }, PreviewCommands.Instance, new UiState<int>(0));

	[UiPreview("All done", Profile = UiPreviewProfiles.Widget)]
	public static UiElement Empty() => Widget(TodoSamples.State(count: 0));

	[UiPreview("Store unavailable", Profile = UiPreviewProfiles.Widget)]
	public static UiElement Unavailable() =>
		Widget(new TodoUiState(TodoSnapshot.Empty, new TodoSettings(), TodoSamples.Now, TodoStoreStatus.Failed));

	private static UiElement Widget(TodoUiState state, TodoWidgetSettings? settings = null) =>
		TodoWidgetView.Build(state, settings ?? TodoWidgetSettings.Default, PreviewCommands.Instance);

	/// <summary>Previews are static pictures; presses have nothing to change.</summary>
	private sealed class PreviewCommands : ITodoCommands
	{
		public static readonly PreviewCommands Instance = new();

		public Task<LocalizedText?> CreateAsync(TodoDraft draft, CancellationToken cancellationToken) => Task.FromResult<LocalizedText?>(null);

		public Task<LocalizedText?> UpdateAsync(Guid id, TodoDraft draft, CancellationToken cancellationToken) => Task.FromResult<LocalizedText?>(null);

		public Task<LocalizedText?> SetCompletedAsync(Guid id, bool completed, CancellationToken cancellationToken) => Task.FromResult<LocalizedText?>(null);

		public Task<LocalizedText?> DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<LocalizedText?>(null);
	}
}
