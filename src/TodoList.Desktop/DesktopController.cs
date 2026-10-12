using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using Serilog;
using TodoList.Core;
using TodoList.Core.Hotkeys;
using TodoList.Core.Scheduling;
using TodoList.Core.Settings;
using TodoList.Core.Storage;
using TodoList.Desktop.Hotkeys;
using TodoList.Desktop.Native.MacOS;
using TodoList.Desktop.Native.Windows;
using TodoList.Desktop.Tray;
using TodoList.Desktop.ViewModels;
using TodoList.Desktop.Views;

namespace TodoList.Desktop;

/// <summary>
/// Owns everything the desktop UI shows: the tray item, the global shortcut and the three windows. Created
/// and used on the Avalonia UI thread only.
/// </summary>
internal sealed class DesktopController : IDisposable
{
	private const int Gap = 6;

	private readonly Application _application;
	private readonly SettingsService _settings;
	private readonly DataLocationService _location;
	private readonly ILogger _logger;
	private readonly TodoListViewModel _model;
	private readonly HotkeyState _hotkeyState = new();
	private readonly bool _isMac = OperatingSystem.IsMacOS();
	private ITrayIcon? _tray;
	private IGlobalHotkey? _hotkey;
	private PopupWindow? _popup;
	private QuickAddWindow? _quickAdd;
	private SettingsWindow? _settingsWindow;
	private readonly MinuteClock _minuteClock;
	private DispatcherTimer? _deactivationCheck;
	private bool _tickingPopup;
	private string? _registeredHotkey;

	public DesktopController(Application application, TodoService todos, SettingsService settings, DataLocationService location, TimeProvider clock, MinuteClock minuteClock, Task storeReady, ILogger logger)
	{
		_application = application;
		_minuteClock = minuteClock;
		_settings = settings;
		_location = location;
		_logger = logger.ForContext<DesktopController>();
		_model = new TodoListViewModel(todos, settings, clock, logger, action => Dispatcher.UIThread.Post(action));
		_ = storeReady.ContinueWith(_ => Dispatcher.UIThread.Post(() => { ApplySettings(); _model.Refresh(); }), TaskScheduler.Default);
		_settings.Changed += OnSettingsChanged;
	}

	public void Start()
	{
		ApplyTheme();
		_tray = TryCreate("tray icon", CreateTray);
		if (_tray is not null)
		{
			_tray.Activated += (_, anchor) => TogglePopup(anchor);
		}

		_hotkey = TryCreate<IGlobalHotkey>("global shortcut", () => _isMac ? new CarbonHotkey(_logger) : new Win32HotkeyWindow(_logger));
		if (_hotkey is not null)
		{
			_hotkey.Pressed += (_, _) => ShowQuickAdd();
		}

		ApplyHotkey();
	}

	public void ShowPopup(PixelRect? anchor = null)
	{
		var popup = Popup();
		_model.Reset();
		Position(popup, anchor, new Size(PopupWindow.PopupWidth, PopupWindow.PopupHeight), quickAdd: false);
		// The popup grows out of the menu bar on macOS and out of the taskbar on Windows.
		Reveal.Play((Visual)popup.Content!, _isMac ? new RelativePoint(0.5, 0, RelativeUnit.Relative) : new RelativePoint(0.5, 1, RelativeUnit.Relative), _isMac ? -8 : 8);
		Present(popup);
		TickPopup(true);
		popup.FocusQuickAdd();
	}

	public void ShowQuickAdd()
	{
		_popup?.Dismiss();
		var window = _quickAdd ??= CreateQuickAdd();
		window.Prepare();
		Position(window, anchor: null, new Size(QuickAddWindow.WindowWidth, QuickAddWindow.WindowHeight), quickAdd: true);
		Reveal.Play((Visual)window.Content!, RelativePoint.Center, -4);
		Present(window);
	}

	public void ShowSettings()
	{
		_popup?.Dismiss();
		if (_settingsWindow is null)
		{
			_settingsWindow = new SettingsWindow(_settings, _location, _hotkeyState, SuspendHotkey, _logger);
			_settingsWindow.Closed += (_, _) =>
			{
				_settingsWindow = null;
				HideAppIfIdle();
			};
		}

		Present(_settingsWindow);
	}

	public void Dispose()
	{
		_settings.Changed -= OnSettingsChanged;
		TickPopup(false);
		_deactivationCheck?.Stop();
		_hotkey?.Dispose();
		_tray?.Dispose();
		_popup?.Close();
		_quickAdd?.Close();
		_settingsWindow?.Close();
		_model.Dispose();
	}

	private T? TryCreate<T>(string what, Func<T> create)
		where T : class
	{
		try
		{
			return create();
		}
		catch (Exception exception)
		{
			// The rest of the desktop UI and the plugin keep working without this piece.
			_logger.Error(exception, "Could not create the {What}", what);
			return null;
		}
	}

	private ITrayIcon CreateTray() => _isMac
		? new MacStatusItem(TrayIconImages.MacTemplatePng(36), 18, _logger)
		: new AvaloniaTrayIcon(_application, TrayIconImages.WindowsIcon(32), () => ShowPopup(), ShowQuickAdd, ShowSettings);

	private PopupWindow Popup()
	{
		if (_popup is not null)
		{
			return _popup;
		}

		_popup = new PopupWindow(_model, ShowSettings);
		_popup.Deactivated += (_, _) =>
		{
			if (_settings.Current.ClosePopupOnFocusLoss)
			{
				ScheduleDeactivationCheck(_popup, _popup.Dismiss);
			}
		};
		_popup.Dismissed += (_, _) =>
		{
			TickPopup(false);
			HideAppIfIdle();
		};
		return _popup;
	}

	private QuickAddWindow CreateQuickAdd()
	{
		var window = new QuickAddWindow(_model);
		window.Deactivated += (_, _) => ScheduleDeactivationCheck(window, window.Dismiss);
		window.Dismissed += (_, _) => HideAppIfIdle();
		return window;
	}

	private void TogglePopup(PixelRect? anchor)
	{
		if (_popup is { IsVisible: true })
		{
			_popup.Dismiss();
			return;
		}

		ShowPopup(anchor);
	}

	// Dropdowns and context menus briefly take activation, so only close if the window is still inactive.
	private void ScheduleDeactivationCheck(Window window, Action dismiss)
	{
		_deactivationCheck?.Stop();
		_deactivationCheck = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) =>
		{
			_deactivationCheck?.Stop();
			if (window is { IsVisible: true, IsActive: false })
			{
				dismiss();
			}
		});
		_deactivationCheck.Start();
	}

	// Relative due labels ("in 5 min", overdue) change with time, so a visible popup refreshes every minute.
	private void TickPopup(bool on)
	{
		if (on == _tickingPopup)
		{
			return;
		}

		_tickingPopup = on;
		if (on)
		{
			_minuteClock.Tick += OnMinute;
		}
		else
		{
			_minuteClock.Tick -= OnMinute;
		}
	}

	private void OnMinute(object? sender, DateTimeOffset now) => Dispatcher.UIThread.Post(_model.Refresh);

	/// <summary>Releases the shortcut while the settings window records a new one, so the current one can be pressed.</summary>
	private void SuspendHotkey(bool suspended)
	{
		if (suspended)
		{
			_hotkey?.Unregister();
			_registeredHotkey = null;
		}
		else
		{
			ApplyHotkey();
		}
	}

	private void Present(Window window)
	{
		window.Show();
		window.Activate();

		// An accessory app does not come to the front on its own; without this the window gets no key input.
		if (_isMac)
		{
			MacApplication.Activate();
		}
	}

	private void HideAppIfIdle()
	{
		if (_isMac && _popup?.IsVisible != true && _quickAdd?.IsVisible != true && _settingsWindow is null)
		{
			MacApplication.Hide();
		}
	}

	private void Position(Window window, PixelRect? anchor, Size size, bool quickAdd)
	{
		var screens = window.Screens;
		var reference = anchor?.Center ?? PointerPosition();
		var screen = (reference is { } point ? screens.ScreenFromPoint(point) : null) ?? screens.Primary;
		if (screen is null)
		{
			return;
		}

		// Windows positions windows in physical pixels; macOS in points.
		var scaling = _isMac ? 1 : screen.Scaling;
		var pixels = PixelSize.FromSize(size, scaling);
		window.Position = quickAdd
			? new PixelPoint(
				screen.WorkingArea.X + ((screen.WorkingArea.Width - pixels.Width) / 2),
				screen.WorkingArea.Y + (screen.WorkingArea.Height / 4))
			: PopupPlacement.Compute(anchor, pixels, screen.Bounds, screen.WorkingArea, (int)(Gap * scaling));
	}

	private PixelPoint? PointerPosition()
	{
		if (!_isMac)
		{
			return Win32Cursor.Position();
		}

		var (x, y) = MacApplication.MouseLocation();
		return new PixelPoint((int)x, (int)y);
	}

	private void OnSettingsChanged(object? sender, TodoSettings settings) => Dispatcher.UIThread.Post(ApplySettings);

	private void ApplySettings()
	{
		ApplyTheme();
		ApplyHotkey();
	}

	private void ApplyTheme() => _application.RequestedThemeVariant = _settings.Current.Theme switch
	{
		ThemePreference.Light => ThemeVariant.Light,
		ThemePreference.Dark => ThemeVariant.Dark,
		_ => ThemeVariant.Default,
	};

	private void ApplyHotkey()
	{
		if (_hotkey is null)
		{
			_hotkeyState.Set(HotkeyRegistrationResult.Unsupported);
			return;
		}

		var current = _settings.Current;
		if (!current.HotkeyEnabled)
		{
			_hotkey.Unregister();
			_registeredHotkey = null;
			_hotkeyState.Set(null);
			return;
		}

		if (current.Hotkey == _registeredHotkey && _hotkeyState.Result == HotkeyRegistrationResult.Registered)
		{
			return;
		}

		if (!HotkeyGesture.TryParse(current.Hotkey, out var gesture) || gesture.Validate(_isMac) != HotkeyValidationError.None)
		{
			_hotkey.Unregister();
			_registeredHotkey = null;
			_hotkeyState.Set(HotkeyRegistrationResult.Invalid);
			return;
		}

		var result = _hotkey.Register(gesture);
		_registeredHotkey = result == HotkeyRegistrationResult.Registered ? current.Hotkey : null;
		_hotkeyState.Set(result);
		_logger.Information("Global shortcut {Gesture}: {Result}", current.Hotkey, result);
	}
}
