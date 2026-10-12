using Avalonia;

namespace TodoList.Desktop.Tray;

/// <summary>
/// The tray or menu bar item. <see cref="Activated"/> carries where the item is on screen, in the
/// coordinates Avalonia uses to position windows, or null when the platform cannot tell.
/// </summary>
internal interface ITrayIcon : IDisposable
{
	event EventHandler<PixelRect?>? Activated;
}
