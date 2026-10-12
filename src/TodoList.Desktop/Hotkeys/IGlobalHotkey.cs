using TodoList.Core.Hotkeys;

namespace TodoList.Desktop.Hotkeys;

public enum HotkeyRegistrationResult
{
	Registered,
	InUse,
	Invalid,
	Unsupported,
	Failed,
}

/// <summary>
/// One registered system-wide shortcut. Registration replaces the previous one. Used on the UI thread only.
/// </summary>
internal interface IGlobalHotkey : IDisposable
{
	event EventHandler? Pressed;

	HotkeyRegistrationResult Register(HotkeyGesture gesture);

	void Unregister();
}
