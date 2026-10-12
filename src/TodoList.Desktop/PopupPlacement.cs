using Avalonia;

namespace TodoList.Desktop;

/// <summary>
/// Where a popover goes next to a tray or menu bar item. All values are in the same screen coordinates.
/// </summary>
internal static class PopupPlacement
{
	public enum Edge
	{
		Top,
		Bottom,
		Left,
		Right,
	}

	/// <summary>
	/// Places the popup against the edge that holds the taskbar or menu bar, centered on the anchor, and keeps
	/// it inside the screen's work area. Without an anchor the popup goes to the corner where the tray usually is.
	/// </summary>
	public static PixelPoint Compute(PixelRect? anchor, PixelSize popup, PixelRect bounds, PixelRect workArea, int gap)
	{
		var edge = BarEdge(anchor, bounds, workArea);
		var target = anchor ?? DefaultAnchor(edge, workArea);
		var centerX = target.X + (target.Width / 2);
		var centerY = target.Y + (target.Height / 2);

		var (x, y) = edge switch
		{
			Edge.Top => (centerX - (popup.Width / 2), Math.Max(target.Bottom, workArea.Y) + gap),
			Edge.Bottom => (centerX - (popup.Width / 2), Math.Min(target.Y, workArea.Bottom) - popup.Height - gap),
			Edge.Left => (Math.Max(target.Right, workArea.X) + gap, centerY - (popup.Height / 2)),
			_ => (Math.Min(target.X, workArea.Right) - popup.Width - gap, centerY - (popup.Height / 2)),
		};

		return new PixelPoint(
			Clamp(x, workArea.X + gap, workArea.Right - popup.Width - gap),
			Clamp(y, workArea.Y + gap, workArea.Bottom - popup.Height - gap));
	}

	/// <summary>The edge whose bar the anchor belongs to: the side where the work area is inset, else the nearest.</summary>
	public static Edge BarEdge(PixelRect? anchor, PixelRect bounds, PixelRect workArea)
	{
		var insets = new (Edge Edge, int Inset)[]
		{
			(Edge.Bottom, bounds.Bottom - workArea.Bottom),
			(Edge.Top, workArea.Y - bounds.Y),
			(Edge.Left, workArea.X - bounds.X),
			(Edge.Right, bounds.Right - workArea.Right),
		};

		if (anchor is not { } a)
		{
			var inset = insets.MaxBy(i => i.Inset);
			return inset.Inset > 0 ? inset.Edge : Edge.Bottom;
		}

		// An auto-hidden taskbar leaves no inset, so the edge closest to the anchor decides.
		var distances = new (Edge Edge, int Distance)[]
		{
			(Edge.Bottom, bounds.Bottom - a.Bottom),
			(Edge.Top, a.Y - bounds.Y),
			(Edge.Left, a.X - bounds.X),
			(Edge.Right, bounds.Right - a.Right),
		};
		return distances.MinBy(d => d.Distance).Edge;
	}

	// A point just outside the work area on the bar's edge, where the tray usually sits.
	private static PixelRect DefaultAnchor(Edge edge, PixelRect workArea) => edge switch
	{
		Edge.Top => new PixelRect(workArea.Right - 1, workArea.Y - 1, 1, 1),
		Edge.Left => new PixelRect(workArea.X - 1, workArea.Bottom - 1, 1, 1),
		Edge.Right => new PixelRect(workArea.Right, workArea.Bottom - 1, 1, 1),
		_ => new PixelRect(workArea.Right - 1, workArea.Bottom, 1, 1),
	};

	private static int Clamp(int value, int min, int max) => max < min ? min : Math.Clamp(value, min, max);
}
