namespace TodoList.Desktop.Hotkeys;

/// <summary>
/// What happened to the last shortcut registration, for the settings window. Used on the UI thread.
/// </summary>
internal sealed class HotkeyState
{
	/// <summary>Null while the shortcut is disabled.</summary>
	public HotkeyRegistrationResult? Result { get; private set; }

	public event EventHandler? Changed;

	public void Set(HotkeyRegistrationResult? result)
	{
		Result = result;
		Changed?.Invoke(this, EventArgs.Empty);
	}
}
