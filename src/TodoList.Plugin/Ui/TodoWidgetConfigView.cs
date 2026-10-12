using MacroDeck.Ui.Config;
using MacroDeck.Ui.Config.Options;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Runtime;

namespace TodoList.Plugin.Ui;

/// <summary>
/// The widget's settings page. Each input's key is the data key it writes; the host saves and validates the
/// draft and reopens the widget with it.
/// </summary>
internal static class TodoWidgetConfigView
{
	public static UiElement Build(TodoWidgetSettings current) => new UiWidgetConfiguration
	{
		Key = "todo-widget-config",
		Properties = new UiWidgetProperties
		{
			Key = "properties",
			Children =
			[
				new UiNumberInput
				{
					Key = TodoWidgetSettings.MaxVisibleKey,
					Label = Strings.WidgetConfig.MaxVisible.Label(),
					Description = Strings.WidgetConfig.MaxVisible.Description(),
					Min = TodoWidgetSettings.MinVisible,
					Max = TodoWidgetSettings.MaxVisibleLimit,
					Step = 1,
					ShowSlider = true,
					DefaultValue = TodoWidgetSettings.Default.MaxVisible,
					Binding = Bind.To(new UiState<double>(current.MaxVisible)),
				},
				new UiChoiceInput
				{
					Key = TodoWidgetSettings.SortKey,
					Label = Strings.WidgetConfig.Sort.Label(),
					DefaultValue = TodoWidgetSettings.SortValue(TodoWidgetSettings.Default.Sort),
					Options = UiValue.Of<IReadOnlyList<UiOption>>(
					[
						UiOption.Of("due", Strings.Sort.DueDate()),
						UiOption.Of("created", Strings.Sort.CreatedDate()),
						UiOption.Of("manual", Strings.Sort.Manual()),
					]),
					Binding = Bind.To(new UiState<string>(TodoWidgetSettings.SortValue(current.Sort))),
				},
				new UiChoiceInput
				{
					Key = TodoWidgetSettings.StatusFilterKey,
					Label = Strings.WidgetConfig.Filter.Label(),
					DefaultValue = TodoWidgetSettings.FilterValue(TodoWidgetSettings.Default.StatusFilter),
					Options = UiValue.Of<IReadOnlyList<UiOption>>(
					[
						UiOption.Of("all", Strings.WidgetConfig.Filter.All()),
						UiOption.Of("due", Strings.WidgetConfig.Filter.Due()),
						UiOption.Of("overdue", Strings.WidgetConfig.Filter.Overdue()),
						UiOption.Of("due-or-overdue", Strings.WidgetConfig.Filter.DueOrOverdue()),
					]),
					Binding = Bind.To(new UiState<string>(TodoWidgetSettings.FilterValue(current.StatusFilter))),
				},
				Toggle(TodoWidgetSettings.ShowDueDatesKey, Strings.WidgetConfig.ShowDueDates(), current.ShowDueDates, TodoWidgetSettings.Default.ShowDueDates),
				Toggle(TodoWidgetSettings.OverdueFirstKey, Strings.WidgetConfig.OverdueFirst(), current.OverdueFirst, TodoWidgetSettings.Default.OverdueFirst),
				Toggle(TodoWidgetSettings.ShowCompletedKey, Strings.WidgetConfig.ShowCompleted(), current.ShowCompleted, TodoWidgetSettings.Default.ShowCompleted),
				Toggle(
					TodoWidgetSettings.InteractiveKey,
					Strings.WidgetConfig.Interactive.Label(),
					current.Interactive,
					TodoWidgetSettings.Default.Interactive,
					Strings.WidgetConfig.Interactive.Description()),
			],
		},
	};

	private static UiBooleanInput Toggle(string key, UiText label, bool value, bool defaultValue, UiText description = default) => new()
	{
		Key = key,
		Label = label,
		Description = description,
		DefaultValue = defaultValue,
		Binding = Bind.To(new UiState<bool>(value)),
	};
}
