using System.Runtime.InteropServices;
using TodoList.Core.Hotkeys;
using TodoList.Desktop.Hotkeys;

namespace TodoList.Desktop.Native.Windows;

/// <summary>
/// A system-wide shortcut through RegisterHotKey. WM_HOTKEY goes to a message-only window created on the
/// Avalonia UI thread, whose message loop already pumps it. No keyboard hook is installed.
/// </summary>
internal sealed partial class Win32HotkeyWindow : IGlobalHotkey
{
	private const int HotkeyId = 0x5444;
	private const uint WmHotkey = 0x0312;
	private const uint ModAlt = 0x0001;
	private const uint ModControl = 0x0002;
	private const uint ModShift = 0x0004;
	private const uint ModWin = 0x0008;
	private const uint ModNoRepeat = 0x4000;
	private const int ErrorHotkeyAlreadyRegistered = 1409;
	private static readonly IntPtr MessageOnlyParent = new(-3);

	private readonly Serilog.ILogger _logger;
	private readonly WindowProc _windowProc;
	private readonly string _className = "TodoListHotkeyWindow" + Environment.ProcessId;
	private IntPtr _window;
	private bool _registered;

	public Win32HotkeyWindow(Serilog.ILogger logger)
	{
		_logger = logger.ForContext<Win32HotkeyWindow>();

		// Kept in a field so the delegate outlives every message Windows sends to it.
		_windowProc = Procedure;
		var windowClass = new WindowClassEx
		{
			Size = (uint)Marshal.SizeOf<WindowClassEx>(),
			WindowProc = Marshal.GetFunctionPointerForDelegate(_windowProc),
			Instance = GetModuleHandleW(IntPtr.Zero),
			ClassName = _className,
		};

		if (RegisterClassExW(ref windowClass) == 0)
		{
			_logger.Warning("Registering the hotkey window class failed with {Error}", Marshal.GetLastPInvokeError());
			return;
		}

		_window = CreateWindowExW(0, _className, string.Empty, 0, 0, 0, 0, 0, MessageOnlyParent, IntPtr.Zero, windowClass.Instance, IntPtr.Zero);
		if (_window == IntPtr.Zero)
		{
			_logger.Warning("Creating the hotkey window failed with {Error}", Marshal.GetLastPInvokeError());
		}
	}

	public event EventHandler? Pressed;

	public HotkeyRegistrationResult Register(HotkeyGesture gesture)
	{
		ArgumentNullException.ThrowIfNull(gesture);
		Unregister();

		if (_window == IntPtr.Zero)
		{
			return HotkeyRegistrationResult.Unsupported;
		}

		if (!TryGetVirtualKey(gesture.Key, out var virtualKey))
		{
			return HotkeyRegistrationResult.Invalid;
		}

		if (RegisterHotKey(_window, HotkeyId, Modifiers(gesture.Modifiers) | ModNoRepeat, virtualKey))
		{
			_registered = true;
			return HotkeyRegistrationResult.Registered;
		}

		var error = Marshal.GetLastPInvokeError();
		_logger.Warning("Registering the shortcut {Gesture} failed with {Error}", gesture.ToString(), error);
		return error == ErrorHotkeyAlreadyRegistered ? HotkeyRegistrationResult.InUse : HotkeyRegistrationResult.Failed;
	}

	public void Unregister()
	{
		if (_registered)
		{
			UnregisterHotKey(_window, HotkeyId);
			_registered = false;
		}
	}

	public void Dispose()
	{
		Unregister();
		if (_window != IntPtr.Zero)
		{
			DestroyWindow(_window);
			_window = IntPtr.Zero;
			UnregisterClassW(_className, GetModuleHandleW(IntPtr.Zero));
		}
	}

	internal static bool TryGetVirtualKey(string key, out uint virtualKey)
	{
		virtualKey = 0;
		if (key == "Space")
		{
			virtualKey = 0x20;
			return true;
		}

		if (key.Length == 1 && key[0] is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
		{
			// Letters and digits share their ASCII code with the virtual key.
			virtualKey = key[0];
			return true;
		}

		if (key.StartsWith('F') && int.TryParse(key.AsSpan(1), out var number) && number is >= 1 and <= 24)
		{
			virtualKey = (uint)(0x70 + number - 1);
			return true;
		}

		return false;
	}

	private static uint Modifiers(HotkeyModifiers modifiers) =>
		(modifiers.HasFlag(HotkeyModifiers.Control) ? ModControl : 0) |
		(modifiers.HasFlag(HotkeyModifiers.Alt) ? ModAlt : 0) |
		(modifiers.HasFlag(HotkeyModifiers.Shift) ? ModShift : 0) |
		(modifiers.HasFlag(HotkeyModifiers.Meta) ? ModWin : 0);

	private IntPtr Procedure(IntPtr window, uint message, IntPtr wParam, IntPtr lParam)
	{
		if (message == WmHotkey && wParam == HotkeyId)
		{
			try
			{
				Pressed?.Invoke(this, EventArgs.Empty);
			}
			catch (Exception exception)
			{
				_logger.Error(exception, "Handling the global shortcut failed");
			}

			return IntPtr.Zero;
		}

		return DefWindowProcW(window, message, wParam, lParam);
	}

	[UnmanagedFunctionPointer(CallingConvention.Winapi)]
	private delegate IntPtr WindowProc(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	private struct WindowClassEx
	{
		public uint Size;
		public uint Style;
		public IntPtr WindowProc;
		public int ClassExtra;
		public int WindowExtra;
		public IntPtr Instance;
		public IntPtr Icon;
		public IntPtr Cursor;
		public IntPtr Background;
		public string? MenuName;
		public string ClassName;
		public IntPtr SmallIcon;
	}

	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	private static extern ushort RegisterClassExW(ref WindowClassEx windowClass);

	[LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool UnregisterClassW(string className, IntPtr instance);

	[LibraryImport("user32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr CreateWindowExW(uint exStyle, string className, string windowName, uint style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

	[LibraryImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool DestroyWindow(IntPtr window);

	[LibraryImport("user32.dll")]
	private static partial IntPtr DefWindowProcW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

	[LibraryImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);

	[LibraryImport("user32.dll", SetLastError = true)]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool UnregisterHotKey(IntPtr window, int id);

	[LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16)]
	private static partial IntPtr GetModuleHandleW(IntPtr moduleName);
}
