using TodoList.Core.Hotkeys;
using TodoList.Core.Input;

namespace TodoList.Core.Settings;

public enum ThemePreference
{
	System,
	Light,
	Dark,
}

/// <summary>
/// User preferences, edited in the desktop settings window and stored in the plugin database.
/// </summary>
public sealed record TodoSettings
{
	public const int MaxThresholdMinutes = 7 * 24 * 60;

	public bool HotkeyEnabled { get; init; } = true;

	public string Hotkey { get; init; } = HotkeyGesture.Default.ToString();

	public ThemePreference Theme { get; init; } = ThemePreference.System;

	public TodoSortOrder DefaultSort { get; init; } = TodoSortOrder.DueDate;

	/// <summary>The due date new todos get when the user does not pick one.</summary>
	public DuePreset DefaultDue { get; init; } = DuePreset.None;

	public int DueSoonMinutes { get; init; } = 60;

	public int OverdueGraceMinutes { get; init; }

	public bool ShowCompleted { get; init; }

	/// <summary>Hide the tray popup when it loses focus, like a native popover.</summary>
	public bool ClosePopupOnFocusLoss { get; init; } = true;

	/// <summary>Reopen the popup with the previous selection and filter instead of a fresh list.</summary>
	public bool RememberPopupState { get; init; } = true;

	public bool ConfirmDelete { get; init; } = true;

	public TodoThresholds Thresholds =>
		new(TimeSpan.FromMinutes(DueSoonMinutes), TimeSpan.FromMinutes(OverdueGraceMinutes));

	/// <summary>Clamps out-of-range values and replaces an unparsable hotkey with the default.</summary>
	public TodoSettings Normalize() => this with
	{
		Hotkey = HotkeyGesture.TryParse(Hotkey, out var gesture) ? gesture.ToString() : HotkeyGesture.Default.ToString(),
		Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.System,
		DefaultSort = Enum.IsDefined(DefaultSort) ? DefaultSort : TodoSortOrder.DueDate,
		DefaultDue = Enum.IsDefined(DefaultDue) ? DefaultDue : DuePreset.None,
		DueSoonMinutes = Math.Clamp(DueSoonMinutes, 0, MaxThresholdMinutes),
		OverdueGraceMinutes = Math.Clamp(OverdueGraceMinutes, 0, MaxThresholdMinutes),
	};
}
