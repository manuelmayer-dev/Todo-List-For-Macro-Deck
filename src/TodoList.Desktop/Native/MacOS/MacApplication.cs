namespace TodoList.Desktop.Native.MacOS;

/// <summary>
/// App-level AppKit calls for an accessory app without a Dock icon.
/// </summary>
internal static class MacApplication
{
	private static IntPtr SharedApplication => ObjC.ClassSend("NSApplication", "sharedApplication");

	/// <summary>Brings this app to the front so its window receives keyboard input.</summary>
	public static void Activate() =>
		ObjC.SendBool(SharedApplication, ObjC.Selector("activateIgnoringOtherApps:"), true);

	/// <summary>Hides the app, which hands focus back to the app that was active before.</summary>
	public static void Hide() => ObjC.Send(SharedApplication, ObjC.Selector("hide:"), IntPtr.Zero);

	/// <summary>The pointer position in points with a top-left origin.</summary>
	public static (double X, double Y) MouseLocation()
	{
		var location = ObjC.SendReturnPoint(ObjC.GetClass("NSEvent"), ObjC.Selector("mouseLocation"));
		return (location.X, PrimaryScreenHeight() - location.Y);
	}

	/// <summary>The height of the primary screen in points, needed to flip Cocoa's bottom-left origin.</summary>
	public static double PrimaryScreenHeight()
	{
		var screens = ObjC.ClassSend("NSScreen", "screens");
		var primary = ObjC.Send(screens, ObjC.Selector("firstObject"));
		return primary == IntPtr.Zero ? 0 : ObjC.SendReturnRect(primary, ObjC.Selector("frame")).Height;
	}
}
