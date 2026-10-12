using Avalonia.Media;
using Avalonia.Styling;

namespace TodoList.Desktop.Theme;

/// <summary>
/// Colours in the style of macOS system colours and launcher apps like Raycast: quiet surfaces,
/// hairline separators, three label levels and the system accent.
/// </summary>
internal sealed record Tokens(
	Color Window,
	Color Surface,
	Color Control,
	Color ControlHover,
	Color Selection,
	Color Separator,
	Color Keycap,
	Color LabelPrimary,
	Color LabelSecondary,
	Color LabelTertiary,
	Color Red,
	Color RedTint,
	Color Orange,
	Color OrangeTint,
	Color Green,
	Color SwitchOff,
	Color DefaultAccent)
{
	public static readonly Tokens Dark = new(
		Window: Color.Parse("#FF1C1C1E"),
		Surface: Color.Parse("#FF242426"),
		Control: Color.Parse("#14FFFFFF"),
		ControlHover: Color.Parse("#22FFFFFF"),
		Selection: Color.Parse("#1AFFFFFF"),
		Separator: Color.Parse("#17FFFFFF"),
		Keycap: Color.Parse("#1FFFFFFF"),
		LabelPrimary: Color.Parse("#F5F5F7"),
		LabelSecondary: Color.Parse("#98989F"),
		LabelTertiary: Color.Parse("#636366"),
		Red: Color.Parse("#FF453A"),
		RedTint: Color.Parse("#2EFF453A"),
		Orange: Color.Parse("#FF9F0A"),
		OrangeTint: Color.Parse("#2EFF9F0A"),
		Green: Color.Parse("#30D158"),
		SwitchOff: Color.Parse("#33FFFFFF"),
		DefaultAccent: Color.Parse("#0A84FF"));

	public static readonly Tokens Light = new(
		Window: Color.Parse("#FFF6F6F6"),
		Surface: Color.Parse("#FFFFFFFF"),
		Control: Color.Parse("#0F000000"),
		ControlHover: Color.Parse("#17000000"),
		Selection: Color.Parse("#12000000"),
		Separator: Color.Parse("#14000000"),
		Keycap: Color.Parse("#14000000"),
		LabelPrimary: Color.Parse("#1D1D1F"),
		LabelSecondary: Color.Parse("#6E6E73"),
		LabelTertiary: Color.Parse("#AEAEB2"),
		Red: Color.Parse("#FF3B30"),
		RedTint: Color.Parse("#1FFF3B30"),
		Orange: Color.Parse("#C26D00"),
		OrangeTint: Color.Parse("#1FFF9500"),
		Green: Color.Parse("#34C759"),
		SwitchOff: Color.Parse("#1F000000"),
		DefaultAccent: Color.Parse("#007AFF"));

	public static Tokens For(ThemeVariant variant) => variant == ThemeVariant.Light ? Light : Dark;
}
