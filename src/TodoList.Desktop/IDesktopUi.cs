namespace TodoList.Desktop;

/// <summary>
/// What the rest of the plugin may ask of the desktop UI. Every member is safe to call from any thread and
/// does nothing harmful when the desktop UI is not running.
/// </summary>
public interface IDesktopUi
{
	bool IsAvailable { get; }

	/// <summary>Opens the tray popup. Returns false when the desktop UI is unavailable.</summary>
	bool ShowTodoPopup();

	/// <summary>Opens the quick-add window. Returns false when the desktop UI is unavailable.</summary>
	bool ShowQuickAdd();
}
