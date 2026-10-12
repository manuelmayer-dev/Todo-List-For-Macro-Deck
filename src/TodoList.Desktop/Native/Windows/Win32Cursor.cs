using System.Runtime.InteropServices;
using Avalonia;

namespace TodoList.Desktop.Native.Windows;

internal static partial class Win32Cursor
{
	/// <summary>The pointer position in physical screen pixels.</summary>
	public static PixelPoint? Position() => GetCursorPos(out var point) ? new PixelPoint(point.X, point.Y) : null;

	[StructLayout(LayoutKind.Sequential)]
	private struct Point
	{
		public int X;
		public int Y;
	}

	[LibraryImport("user32.dll")]
	[return: MarshalAs(UnmanagedType.Bool)]
	private static partial bool GetCursorPos(out Point point);
}
