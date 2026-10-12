using System.Globalization;
using MacroDeck.Localization;

namespace TodoList.Desktop.Localization;

/// <summary>
/// Desktop UI text from <c>DesktopStrings.resx</c> through Macro Deck's localization catalog, resolved for the
/// operating system's UI language with the same fallback chain Macro Deck uses (culture, neutral, English).
/// The host does not tell plugins its own language, so the desktop windows follow the system.
/// </summary>
internal static class DesktopText
{
	private static readonly LocalizationResolver Resolver = CreateResolver();

	public static string AppName => Of(DesktopStrings.App.Name());

	public static string Settings => Of(DesktopStrings.Popup.Settings());

	public static string QuickAddPlaceholder => Of(DesktopStrings.Input.AddPlaceholder());

	public static string DueNone => Of(DesktopStrings.Due.Preset.None());

	public static string DueInOneHour => Of(DesktopStrings.Due.Preset.InOneHour());

	public static string DueTodayEvening => Of(DesktopStrings.Due.Preset.TodayEvening());

	public static string DueTomorrowMorning => Of(DesktopStrings.Due.Preset.TomorrowMorning());

	public static string DueNextWeek => Of(DesktopStrings.Due.Preset.NextWeek());

	public static string DueCustom => Of(DesktopStrings.Due.Preset.Custom());

	public static string ShowCompleted => Of(DesktopStrings.Popup.ShowCompleted());

	public static string EmptyList => Of(DesktopStrings.Popup.Empty.Open());

	public static string EmptyCompleted => Of(DesktopStrings.Popup.Empty.Completed());

	public static string EmptyHint => Of(DesktopStrings.Popup.Empty.Hint());

	public static string Loading => Of(DesktopStrings.Popup.Loading());

	public static string StoreUnavailable => Of(DesktopStrings.Errors.StoreUnavailable());

	public static string StoreFailed => Of(DesktopStrings.Errors.StoreFailed());

	public static string TitleRequired => Of(DesktopStrings.Errors.TitleRequired());

	public static string TitleTooLong(int max) => Of(DesktopStrings.Errors.TitleTooLong(max));

	public static string DescriptionTooLong(int max) => Of(DesktopStrings.Errors.NotesTooLong(max));

	public static string TodoNotFound => Of(DesktopStrings.Errors.TodoNotFound());

	public static string Overdue => Of(DesktopStrings.Due.Overdue());

	public static string DueInMinutes(int minutes) => Of(DesktopStrings.Due.InMinutes(minutes));

	public static string DueToday(string time) => Of(DesktopStrings.Due.Today(time));

	public static string DueTomorrow(string time) => Of(DesktopStrings.Due.Tomorrow(time));

	public static string Edit => Of(DesktopStrings.Menu.Edit());

	public static string Delete => Of(DesktopStrings.Menu.Delete());

	public static string Complete => Of(DesktopStrings.Menu.Complete());

	public static string Reopen => Of(DesktopStrings.Menu.Reopen());

	public static string MoveUp => Of(DesktopStrings.Menu.MoveUp());

	public static string MoveDown => Of(DesktopStrings.Menu.MoveDown());

	public static string ConfirmDelete(string title) => Of(DesktopStrings.Popup.ConfirmDelete(title));

	public static string EditTitle => Of(DesktopStrings.Edit.Title());

	public static string EditNotes => Of(DesktopStrings.Edit.Notes());

	public static string EditDueDate => Of(DesktopStrings.Edit.DueDate());

	public static string SettingsTitle => Of(DesktopStrings.Settings.Title());

	public static string SectionShortcut => Of(DesktopStrings.Settings.Shortcut.Section());

	public static string SectionShortcutHint => Of(DesktopStrings.Settings.Shortcut.Hint());

	public static string HotkeyEnabled => Of(DesktopStrings.Settings.Shortcut.Enabled());

	public static string HotkeyRecord => Of(DesktopStrings.Settings.Shortcut.Record());

	public static string HotkeyRecording => Of(DesktopStrings.Settings.Shortcut.Recording());

	public static string HotkeyRegistered => Of(DesktopStrings.Settings.Shortcut.Status.Active());

	public static string HotkeyInUse => Of(DesktopStrings.Settings.Shortcut.Status.InUse());

	public static string HotkeyInvalid => Of(DesktopStrings.Settings.Shortcut.Status.Invalid());

	public static string HotkeyOptionOnly => Of(DesktopStrings.Settings.Shortcut.Status.OptionOnly());

	public static string HotkeyDisabled => Of(DesktopStrings.Settings.Shortcut.Status.Disabled());

	public static string HotkeyUnsupported => Of(DesktopStrings.Settings.Shortcut.Status.Unsupported());

	public static string HotkeyFailed => Of(DesktopStrings.Settings.Shortcut.Status.Failed());

	public static string SectionAppearance => Of(DesktopStrings.Settings.Appearance.Section());

	public static string Theme => Of(DesktopStrings.Settings.Appearance.Theme());

	public static string ThemeSystem => Of(DesktopStrings.Settings.Appearance.ThemeSystem());

	public static string ThemeLight => Of(DesktopStrings.Settings.Appearance.ThemeLight());

	public static string ThemeDark => Of(DesktopStrings.Settings.Appearance.ThemeDark());

	public static string SectionTodos => Of(DesktopStrings.Settings.Todos.Section());

	public static string SectionDueHint => Of(DesktopStrings.Settings.Todos.Hint());

	public static string DefaultSort => Of(DesktopStrings.Settings.Todos.Sort());

	public static string SortDueDate => Of(DesktopStrings.Settings.Todos.SortDueDate());

	public static string SortCreated => Of(DesktopStrings.Settings.Todos.SortCreated());

	public static string SortManual => Of(DesktopStrings.Settings.Todos.SortManual());

	public static string DefaultDue => Of(DesktopStrings.Settings.Todos.DefaultDue());

	public static string DueSoonLabel => Of(DesktopStrings.Settings.Todos.DueSoon());

	public static string OverdueLabel => Of(DesktopStrings.Settings.Todos.Overdue());

	public static string ShowCompletedSetting => Of(DesktopStrings.Settings.Todos.ShowCompleted());

	public static string ConfirmDeleteSetting => Of(DesktopStrings.Settings.Todos.ConfirmDelete());

	public static string SectionPopup => Of(DesktopStrings.Settings.Popup.Section());

	public static string SectionPopupHint => Of(DesktopStrings.Settings.Popup.Hint());

	public static string CloseOnFocusLoss => Of(DesktopStrings.Settings.Popup.CloseOnFocusLoss());

	public static string RememberState => Of(DesktopStrings.Settings.Popup.RememberState());

	public static string TrayOpen => Of(DesktopStrings.Tray.Open());

	public static string TrayQuickAdd => Of(DesktopStrings.Tray.QuickAdd());

	public static string TraySettings => Of(DesktopStrings.Tray.Settings());

	public static string ShortcutHint(string shortcut) => Of(DesktopStrings.Popup.ShortcutHint(shortcut));

	public static string OpenFooter(int count) => Of(DesktopStrings.Popup.OpenCount(count));

	public static string HintDone => Of(DesktopStrings.Hints.Done());

	public static string HintEdit => Of(DesktopStrings.Hints.Edit());

	public static string HintShowCompleted => Of(DesktopStrings.Hints.ShowCompleted());

	public static string HintSave => Of(DesktopStrings.Hints.Save());

	public static string HintCancel => Of(DesktopStrings.Hints.Cancel());

	public static string HintDelete => Of(DesktopStrings.Hints.Delete());

	public static string HintKeep => Of(DesktopStrings.Hints.Keep());

	public static string HintAdd => Of(DesktopStrings.Hints.Add());

	public static string HintDueField => Of(DesktopStrings.Hints.DueField());

	public static string HintSuggest => Of(DesktopStrings.Hints.Suggest());

	public static string HintChoose => Of(DesktopStrings.Hints.Choose());

	public static string DueFieldPlaceholder => Of(DesktopStrings.Due.Field.Placeholder());

	public static string InvalidDue => Of(DesktopStrings.Due.Field.Invalid());

	public static string NoDue => Of(DesktopStrings.Due.Field.None());

	public static string DueUnknown => Of(DesktopStrings.Due.Field.Unknown());

	public static string PickDate => Of(DesktopStrings.Due.Field.PickDate());

	public static string MinutesNone => Of(DesktopStrings.Settings.Duration.None());

	public static string MinutesOption(int count) => Of(DesktopStrings.Settings.Duration.Minutes(count));

	public static string HoursOption(int count) => Of(DesktopStrings.Settings.Duration.Hours(count));

	public static string DaysOption(int count) => Of(DesktopStrings.Settings.Duration.Days(count));

	public static string SectionStorage => Of(DesktopStrings.Settings.Storage.Section());

	public static string SectionStorageHint => Of(DesktopStrings.Settings.Storage.Hint());

	public static string StorageLocation => Of(DesktopStrings.Settings.Storage.Folder());

	public static string StorageChoose => Of(DesktopStrings.Settings.Storage.Choose());

	public static string StorageDefault => Of(DesktopStrings.Settings.Storage.UseDefault());

	public static string StorageReveal => Of(DesktopStrings.Settings.Storage.Reveal());

	public static string StoragePickerTitle => Of(DesktopStrings.Settings.Storage.PickerTitle());

	public static string StorageExisting => Of(DesktopStrings.Settings.Storage.Existing());

	public static string StorageUseExisting => Of(DesktopStrings.Settings.Storage.UseExisting());

	public static string StorageReplace => Of(DesktopStrings.Settings.Storage.Replace());

	public static string StorageMoved => Of(DesktopStrings.Settings.Storage.Moved());

	public static string StorageUsedExisting => Of(DesktopStrings.Settings.Storage.UsedExisting());

	public static string StorageFailed => Of(DesktopStrings.Settings.Storage.Failed());

	/// <summary>Resolves a generated string for the current UI culture.</summary>
	public static string Of(LocalizedString text) => Resolver.Resolve(text, CultureInfo.CurrentUICulture.Name);

	private static LocalizationResolver CreateResolver()
	{
		var registry = new LocalizationCatalogRegistry();
		registry.Register(DesktopStrings.LocalizationCatalog);
		return new LocalizationResolver(registry);
	}
}
