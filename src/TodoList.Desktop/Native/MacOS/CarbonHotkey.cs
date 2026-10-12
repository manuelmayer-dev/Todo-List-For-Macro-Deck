using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using TodoList.Core.Hotkeys;
using TodoList.Desktop.Hotkeys;

namespace TodoList.Desktop.Native.MacOS;

/// <summary>
/// A system-wide shortcut through Carbon's RegisterEventHotKey. It is delivered by the main run loop that
/// Avalonia already pumps, needs no Accessibility or Input Monitoring permission and observes no other keys.
/// </summary>
internal sealed unsafe partial class CarbonHotkey : IGlobalHotkey
{
	private const string Carbon = "/System/Library/Frameworks/Carbon.framework/Carbon";
	private const uint KeyboardEventClass = 0x6B657962; // 'keyb'
	private const uint HotKeyPressedKind = 5;
	private const uint Signature = 0x54444C53; // 'TDLS'
	private const int HotKeyExistsError = -9878;

	private const uint CmdKey = 0x0100;
	private const uint ShiftKey = 0x0200;
	private const uint OptionKey = 0x0800;
	private const uint ControlKey = 0x1000;

	private static CarbonHotkey? _current;

	private readonly Serilog.ILogger _logger;
	private IntPtr _handler;
	private IntPtr _hotKey;

	public CarbonHotkey(Serilog.ILogger logger)
	{
		_logger = logger.ForContext<CarbonHotkey>();
		_current = this;
		var eventType = new EventTypeSpec(KeyboardEventClass, HotKeyPressedKind);
		delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, int> callback = &OnHotKey;
		var status = InstallEventHandler(GetApplicationEventTarget(), (IntPtr)callback, 1, &eventType, IntPtr.Zero, out _handler);
		if (status != 0)
		{
			_logger.Warning("Installing the Carbon hotkey handler failed with {Status}", status);
			_handler = IntPtr.Zero;
		}
	}

	public event EventHandler? Pressed;

	public HotkeyRegistrationResult Register(HotkeyGesture gesture)
	{
		ArgumentNullException.ThrowIfNull(gesture);
		Unregister();

		if (_handler == IntPtr.Zero)
		{
			return HotkeyRegistrationResult.Unsupported;
		}

		if (!MacKeyCodes.TryGet(gesture.Key, out var keyCode))
		{
			return HotkeyRegistrationResult.Invalid;
		}

		var id = new EventHotKeyId(Signature, 1);
		var status = RegisterEventHotKey(keyCode, Modifiers(gesture.Modifiers), id, GetApplicationEventTarget(), 0, out _hotKey);
		if (status == 0)
		{
			return HotkeyRegistrationResult.Registered;
		}

		_hotKey = IntPtr.Zero;
		_logger.Warning("Registering the shortcut {Gesture} failed with {Status}", gesture.ToString(), status);
		return status == HotKeyExistsError ? HotkeyRegistrationResult.InUse : HotkeyRegistrationResult.Failed;
	}

	public void Unregister()
	{
		if (_hotKey != IntPtr.Zero)
		{
			_ = UnregisterEventHotKey(_hotKey);
			_hotKey = IntPtr.Zero;
		}
	}

	public void Dispose()
	{
		Unregister();
		if (_handler != IntPtr.Zero)
		{
			_ = RemoveEventHandler(_handler);
			_handler = IntPtr.Zero;
		}

		if (_current == this)
		{
			_current = null;
		}
	}

	private static uint Modifiers(HotkeyModifiers modifiers) =>
		(modifiers.HasFlag(HotkeyModifiers.Control) ? ControlKey : 0) |
		(modifiers.HasFlag(HotkeyModifiers.Alt) ? OptionKey : 0) |
		(modifiers.HasFlag(HotkeyModifiers.Shift) ? ShiftKey : 0) |
		(modifiers.HasFlag(HotkeyModifiers.Meta) ? CmdKey : 0);

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static int OnHotKey(IntPtr callRef, IntPtr eventRef, IntPtr userData)
	{
		if (_current is not { } hotkey)
		{
			return 0;
		}

		// An exception must not unwind into the Carbon event loop.
		try
		{
			hotkey.Pressed?.Invoke(hotkey, EventArgs.Empty);
		}
		catch (Exception exception)
		{
			hotkey._logger.Error(exception, "Handling the global shortcut failed");
		}

		return 0;
	}

	[StructLayout(LayoutKind.Sequential)]
	private readonly record struct EventTypeSpec(uint EventClass, uint EventKind);

	[StructLayout(LayoutKind.Sequential)]
	private readonly record struct EventHotKeyId(uint Signature, uint Id);

	[LibraryImport(Carbon)]
	private static partial IntPtr GetApplicationEventTarget();

	[LibraryImport(Carbon)]
	private static partial int InstallEventHandler(IntPtr target, IntPtr handler, nuint numTypes, EventTypeSpec* list, IntPtr userData, out IntPtr handlerRef);

	[LibraryImport(Carbon)]
	private static partial int RemoveEventHandler(IntPtr handlerRef);

	[LibraryImport(Carbon)]
	private static partial int RegisterEventHotKey(uint keyCode, uint modifiers, EventHotKeyId id, IntPtr target, uint options, out IntPtr hotKeyRef);

	[LibraryImport(Carbon)]
	private static partial int UnregisterEventHotKey(IntPtr hotKeyRef);
}
