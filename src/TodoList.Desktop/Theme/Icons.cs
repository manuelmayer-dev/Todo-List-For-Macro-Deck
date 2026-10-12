using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Path = Avalonia.Controls.Shapes.Path;
using Avalonia.Media;

namespace TodoList.Desktop.Theme;

/// <summary>
/// Stroke icons from Lucide (ISC license, see THIRD-PARTY-NOTICES), drawn on
/// the usual 24 by 24 grid with a 2 unit stroke.
/// </summary>
internal static class Icons
{
	public const string Check = "M20 6 9 17l-5-5";
	public const string Plus = "M5 12h14M12 5v14";
	public const string Settings = "M9.671 4.136a2.34 2.34 0 0 1 4.659 0 2.34 2.34 0 0 0 3.319 1.915 2.34 2.34 0 0 1 2.33 4.033 2.34 2.34 0 0 0 0 3.831 2.34 2.34 0 0 1-2.33 4.033 2.34 2.34 0 0 0-3.319 1.915 2.34 2.34 0 0 1-4.659 0 2.34 2.34 0 0 0-3.32-1.915 2.34 2.34 0 0 1-2.33-4.033 2.34 2.34 0 0 0 0-3.831A2.34 2.34 0 0 1 6.35 6.051a2.34 2.34 0 0 0 3.319-1.915M15 12a3 3 0 1 1-6 0 3 3 0 1 1 6 0";
	public const string Trash = "M10 11v6M14 11v6M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2";
	public const string Pencil = "M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497zM15 5l4 4";
	public const string Close = "M18 6 6 18M6 6l12 12";
	public const string Clock = "M22 12a10 10 0 1 1-20 0 10 10 0 1 1 20 0M12 6v6l4 2";
	public const string List = "M3 5h.01M3 12h.01M3 19h.01M8 5h13M8 12h13M8 19h13";
	public const string Calendar = "M8 2v3M16 2v3M5 3h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2M3 9h18";
	public const string Command = "M15 6v12a3 3 0 1 0 3-3H6a3 3 0 1 0 3 3V6a3 3 0 1 0-3 3h12a3 3 0 1 0-3-3";
	public const string Return = "M20 4v7a4 4 0 0 1-4 4H4M9 10l-5 5 5 5";
	public const string Space = "M22 17v1c0 .5-.5 1-1 1H3c-.5 0-1-.5-1-1v-1";
	public const string ArrowUp = "M5 12l7-7 7 7M12 19V5";
	public const string ArrowDown = "M12 5v14M19 12l-7 7-7-7";

	/// <summary>An icon whose stroke follows <paramref name="brushKey"/> in the current theme.</summary>
	public static Viewbox Create(string data, double size, string brushKey = Keys.LabelSecondary)
	{
		var path = new Path
		{
			Data = Geometry.Parse(data),
			StrokeThickness = 2,
			StrokeLineCap = PenLineCap.Round,
			StrokeJoin = PenLineJoin.Round,
			Width = 24,
			Height = 24,
		};
		path.Bind(Shape.StrokeProperty, path.GetResourceObservable(brushKey));
		return new Viewbox { Width = size, Height = size, Child = new Canvas { Width = 24, Height = 24, Children = { path } } };
	}
}
