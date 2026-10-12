using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using TodoList.Desktop.Tray;

namespace TodoList.Desktop.Native.MacOS;

/// <summary>
/// A menu bar item built on NSStatusItem directly. Avalonia's tray icon raises no click event on macOS and
/// does not expose where the item sits, both of which a popover needs.
/// </summary>
internal sealed class MacStatusItem : ITrayIcon
{
	private const double VariableLength = -1;
	private const nuint LeftAndRightMouseUp = (1 << 2) | (1 << 4);
	private const string TargetClassName = "TodoListStatusItemTarget";

	// The Objective-C target calls back into managed code by handle, so only this one instance is reachable.
	private static MacStatusItem? _current;
	private static IntPtr _targetClass;

	private readonly Serilog.ILogger _logger;
	private IntPtr _statusItem;
	private IntPtr _target;

	public MacStatusItem(byte[] templatePng, double pointSize, Serilog.ILogger logger)
	{
		_logger = logger.ForContext<MacStatusItem>();
		_current = this;
		var statusBar = ObjC.ClassSend("NSStatusBar", "systemStatusBar");
		_statusItem = ObjC.Send(statusBar, ObjC.Selector("statusItemWithLength:"), VariableLength);
		ObjC.Send(_statusItem, "retain");

		var button = ObjC.Send(_statusItem, "button");
		var image = CreateTemplateImage(templatePng, pointSize);
		ObjC.Send(button, ObjC.Selector("setImage:"), image);

		// The button retains the image; this balances the alloc.
		ObjC.Send(image, "release");
		ObjC.Send(button, ObjC.Selector("setToolTip:"), ObjC.NSString(Localization.DesktopText.AppName));

		_target = ObjC.Send(ObjC.Send(EnsureTargetClass(), "alloc"), "init");
		ObjC.Send(button, ObjC.Selector("setTarget:"), _target);
		ObjC.Send(button, ObjC.Selector("setAction:"), ObjC.Selector("statusItemClicked:"));
		ObjC.SendNuint(button, ObjC.Selector("sendActionOn:"), LeftAndRightMouseUp);
	}

	public event EventHandler<PixelRect?>? Activated;

	public void Dispose()
	{
		if (_statusItem == IntPtr.Zero)
		{
			return;
		}

		var statusBar = ObjC.ClassSend("NSStatusBar", "systemStatusBar");
		ObjC.Send(statusBar, ObjC.Selector("removeStatusItem:"), _statusItem);
		ObjC.Send(_statusItem, "release");
		ObjC.Send(_target, "release");
		_statusItem = IntPtr.Zero;
		_target = IntPtr.Zero;
		if (_current == this)
		{
			_current = null;
		}
	}

	/// <summary>The item's frame in Cocoa screen coordinates (points, origin at the primary screen's bottom left).</summary>
	private CGRect? ButtonFrame()
	{
		var button = ObjC.Send(_statusItem, "button");
		var window = ObjC.Send(button, "window");
		return window == IntPtr.Zero ? null : ObjC.SendReturnRect(window, ObjC.Selector("frame"));
	}

	private static IntPtr CreateTemplateImage(byte[] png, double pointSize)
	{
		var handle = GCHandle.Alloc(png, GCHandleType.Pinned);
		try
		{
			var data = ObjC.Send(ObjC.GetClass("NSData"), ObjC.Selector("dataWithBytes:length:"), handle.AddrOfPinnedObject(), (nuint)png.Length);
			var image = ObjC.Send(ObjC.Send(ObjC.GetClass("NSImage"), "alloc"), ObjC.Selector("initWithData:"), data);
			ObjC.SendSize(image, ObjC.Selector("setSize:"), new CGSize(pointSize, pointSize));
			ObjC.SendBool(image, ObjC.Selector("setTemplate:"), true);
			return image;
		}
		finally
		{
			handle.Free();
		}
	}

	private static unsafe IntPtr EnsureTargetClass()
	{
		if (_targetClass != IntPtr.Zero)
		{
			return _targetClass;
		}

		var existing = ObjC.GetClass(TargetClassName);
		if (existing != IntPtr.Zero)
		{
			return _targetClass = existing;
		}

		var cls = ObjC.AllocateClassPair(ObjC.GetClass("NSObject"), TargetClassName, 0);
		delegate* unmanaged[Cdecl]<IntPtr, IntPtr, IntPtr, void> callback = &OnClicked;
		ObjC.AddMethod(cls, ObjC.Selector("statusItemClicked:"), (IntPtr)callback, "v@:@");
		ObjC.RegisterClassPair(cls);
		return _targetClass = cls;
	}

	[UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
	private static void OnClicked(IntPtr self, IntPtr selector, IntPtr sender)
	{
		if (_current is not { } item)
		{
			return;
		}

		// An exception must not unwind into AppKit.
		try
		{
			item.Activated?.Invoke(item, item.Anchor());
		}
		catch (Exception exception)
		{
			item._logger.Error(exception, "Handling a menu bar click failed");
		}
	}

	/// <summary>The item's frame in points with a top-left origin, the way Avalonia places windows on macOS.</summary>
	private PixelRect? Anchor()
	{
		if (ButtonFrame() is not { } frame)
		{
			return null;
		}

		var top = MacApplication.PrimaryScreenHeight() - (frame.Y + frame.Height);
		return new PixelRect((int)Math.Round(frame.X), (int)Math.Round(top), (int)Math.Round(frame.Width), (int)Math.Round(frame.Height));
	}
}
