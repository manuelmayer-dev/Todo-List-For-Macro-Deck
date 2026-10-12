using System.Runtime.InteropServices;

namespace TodoList.Desktop.Native.MacOS;

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CGRect(double X, double Y, double Width, double Height);

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CGSize(double Width, double Height);

[StructLayout(LayoutKind.Sequential)]
internal readonly record struct CGPoint(double X, double Y);

/// <summary>
/// Minimal Objective-C runtime access for the few AppKit calls Avalonia does not expose. Only arm64 is
/// supported, where struct returns go through plain objc_msgSend.
/// </summary>
internal static partial class ObjC
{
	private const string Library = "/usr/lib/libobjc.A.dylib";

	[LibraryImport(Library, EntryPoint = "objc_getClass", StringMarshalling = StringMarshalling.Utf8)]
	public static partial IntPtr GetClass(string name);

	[LibraryImport(Library, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
	public static partial IntPtr Selector(string name);

	[LibraryImport(Library, EntryPoint = "objc_allocateClassPair", StringMarshalling = StringMarshalling.Utf8)]
	public static partial IntPtr AllocateClassPair(IntPtr superclass, string name, nint extraBytes);

	[LibraryImport(Library, EntryPoint = "objc_registerClassPair")]
	public static partial void RegisterClassPair(IntPtr cls);

	[LibraryImport(Library, EntryPoint = "class_addMethod", StringMarshalling = StringMarshalling.Utf8)]
	[return: MarshalAs(UnmanagedType.U1)]
	public static partial bool AddMethod(IntPtr cls, IntPtr selector, IntPtr implementation, string types);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial IntPtr Send(IntPtr receiver, IntPtr selector);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr argument);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial IntPtr Send(IntPtr receiver, IntPtr selector, double argument);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial IntPtr Send(IntPtr receiver, IntPtr selector, IntPtr data, nuint length);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial void SendSize(IntPtr receiver, IntPtr selector, CGSize size);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial void SendBool(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.U1)] bool value);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial void SendNuint(IntPtr receiver, IntPtr selector, nuint value);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial nuint SendReturnNuint(IntPtr receiver, IntPtr selector);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial CGRect SendReturnRect(IntPtr receiver, IntPtr selector);

	[LibraryImport(Library, EntryPoint = "objc_msgSend")]
	public static partial CGPoint SendReturnPoint(IntPtr receiver, IntPtr selector);

	public static IntPtr Send(IntPtr receiver, string selector) => Send(receiver, Selector(selector));

	public static IntPtr ClassSend(string className, string selector) => Send(GetClass(className), Selector(selector));

	public static IntPtr NSString(string value)
	{
		var utf8 = Marshal.StringToCoTaskMemUTF8(value);
		try
		{
			return Send(GetClass("NSString"), Selector("stringWithUTF8String:"), utf8);
		}
		finally
		{
			Marshal.FreeCoTaskMem(utf8);
		}
	}
}
