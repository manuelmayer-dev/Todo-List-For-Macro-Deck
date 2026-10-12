using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TodoList.Desktop.Theme;

namespace TodoList.Desktop.Views;

/// <summary>
/// An action hint for the bottom bar in the style of Raycast: a label followed by keycaps. With an action
/// it is clickable, so mouse users get the same commands; it never takes keyboard focus because the key
/// it shows is the keyboard way.
/// </summary>
internal static class KeyHint
{
	private const double CapSize = 18;

	public static bool IsMac { get; } = OperatingSystem.IsMacOS();

	public static string Command => IsMac ? "⌘" : "Ctrl";

	public static string Enter => IsMac ? "↵" : "Enter";

	public static string Space => IsMac ? "␣" : "Space";

	public static string Escape => IsMac ? "esc" : "Esc";

	public static string Tab => IsMac ? "tab" : "Tab";

	public const string Up = "↑";

	public const string Down = "↓";

	// Symbols are drawn as icons: the fonts that carry them sit at different heights than the letters.
	private static readonly Dictionary<string, string> SymbolIcons = new()
	{
		["⌘"] = Icons.Command,
		["↵"] = Icons.Return,
		["␣"] = Icons.Space,
		["↑"] = Icons.ArrowUp,
		["↓"] = Icons.ArrowDown,
	};

	public static Control Create(string label, Action? action, params string[] keys)
	{
		var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
		var text = Ui.Text(label, 12, FontWeight.Medium, Keys.LabelSecondary);
		text.Margin = new Thickness(0, 0, 2, 0);
		row.Children.Add(text);
		foreach (var key in keys)
		{
			row.Children.Add(Cap(key));
		}

		if (action is null)
		{
			return row;
		}

		var button = new Button { Content = row, Padding = new Thickness(6, 3), MinHeight = 0, Focusable = false };
		button.Classes.Add("plain");
		button.Click += (_, _) => action();
		return button;
	}

	/// <summary>One keycap: a fixed-height rounded square (wider for words) with its content centred both ways.</summary>
	public static Border Cap(string key)
	{
		Control content = SymbolIcons.TryGetValue(key, out var icon)
			? Icons.Create(icon, 11, Keys.LabelSecondary)
			: new TextBlock
			{
				Text = key,
				FontSize = 11,
				FontWeight = FontWeight.Medium,
				LineHeight = CapSize,
				TextAlignment = TextAlignment.Center,
			}.Brush(TextBlock.ForegroundProperty, Keys.LabelSecondary);
		content.HorizontalAlignment = HorizontalAlignment.Center;
		content.VerticalAlignment = VerticalAlignment.Center;

		return new Border
		{
			CornerRadius = new CornerRadius(4),
			Height = CapSize,
			MinWidth = CapSize,
			Padding = new Thickness(key.Length > 1 ? 5 : 0, 0),
			VerticalAlignment = VerticalAlignment.Center,
			Child = content,
		}.Brush(Border.BackgroundProperty, Keys.Keycap);
	}
}
