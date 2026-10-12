using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using TodoList.Desktop.Localization;
using TodoList.Desktop.Native.Windows;

namespace TodoList.Desktop.Tray;

/// <summary>
/// The Windows notification area icon. Avalonia's tray icon handles Explorer restarts and raises a click;
/// the pointer position at that moment is the icon's position, which is all the popup needs.
/// </summary>
internal sealed class AvaloniaTrayIcon : ITrayIcon
{
	private readonly Application _application;
	private readonly TrayIcon _icon;
	private readonly TrayIcons _icons;

	public AvaloniaTrayIcon(Application application, Bitmap image, Action openList, Action quickAdd, Action settings)
	{
		_application = application;
		var menu = new NativeMenu();
		menu.Items.Add(MenuItem(DesktopText.TrayOpen, openList));
		menu.Items.Add(MenuItem(DesktopText.TrayQuickAdd, quickAdd));
		menu.Items.Add(new NativeMenuItemSeparator());
		menu.Items.Add(MenuItem(DesktopText.TraySettings, settings));

		_icon = new TrayIcon
		{
			Icon = new WindowIcon(image),
			ToolTipText = DesktopText.AppName,
			Menu = menu,
			IsVisible = true,
		};
		_icon.Clicked += OnClicked;
		_icons = [_icon];
		TrayIcon.SetIcons(application, _icons);
	}

	public event EventHandler<PixelRect?>? Activated;

	public void Dispose()
	{
		_icon.Clicked -= OnClicked;
		_icon.IsVisible = false;
		_icon.Dispose();
		TrayIcon.SetIcons(_application, []);
	}

	private void OnClicked(object? sender, EventArgs e) =>
		Activated?.Invoke(this, Win32Cursor.Position() is { } point ? new PixelRect(point, new PixelSize(1, 1)) : null);

	private static NativeMenuItem MenuItem(string header, Action action)
	{
		var item = new NativeMenuItem(header);
		item.Click += (_, _) => action();
		return item;
	}
}
