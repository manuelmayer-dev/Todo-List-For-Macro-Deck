using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using TodoList.Core.Input;
using TodoList.Desktop.Localization;
using TodoList.Desktop.Theme;

namespace TodoList.Desktop.Views;

/// <summary>
/// Small factory helpers so the code-built views stay readable and use the theme brushes.
/// </summary>
internal static class Ui
{
	public const double Small = 12;
	public const double Body = 13;

	/// <summary>Binds <paramref name="property"/> to a theme brush so it follows light and dark mode.</summary>
	public static T Brush<T>(this T control, AvaloniaProperty property, string key)
		where T : Control
	{
		control.Bind(property, control.GetResourceObservable(key));
		return control;
	}

	public static TextBlock Text(string text, double size = Body, FontWeight weight = FontWeight.Normal, string brush = Keys.LabelPrimary) =>
		new TextBlock
		{
			Text = text,
			FontSize = size,
			FontWeight = weight,
			VerticalAlignment = VerticalAlignment.Center,
			TextTrimming = TextTrimming.CharacterEllipsis,
		}.Brush(TextBlock.ForegroundProperty, brush);

	public static TextBlock Wrapping(this TextBlock text)
	{
		text.TextWrapping = TextWrapping.Wrap;
		text.TextTrimming = TextTrimming.None;
		return text;
	}

	public static Button Button(string text, Action click, string? variant = null)
	{
		var button = new Button { Content = text, VerticalAlignment = VerticalAlignment.Center };
		if (variant is not null)
		{
			button.Classes.Add(variant);
		}

		button.Click += (_, _) => click();
		return button;
	}

	public static Button IconButton(string icon, string tooltip, Action click)
	{
		var button = new Button { Content = Icons.Create(icon, 15, Keys.LabelSecondary), VerticalAlignment = VerticalAlignment.Center };
		button.Classes.Add("plain");
		button.Classes.Add("icon");
		ToolTip.SetTip(button, tooltip);
		button.Click += (_, _) => click();
		return button;
	}

	/// <summary>A small rounded tag, tinted when it carries a state (overdue, due soon).</summary>
	public static Border Tag(string text, string foreground, string? tint)
	{
		var tag = new Border
		{
			CornerRadius = new CornerRadius(5),
			Padding = tint is null ? new Thickness(0) : new Thickness(6, 1),
			VerticalAlignment = VerticalAlignment.Center,
			Child = Text(text, Small, FontWeight.Medium, foreground),
		};
		return tint is null ? tag : tag.Brush(Border.BackgroundProperty, tint);
	}

	public static Border Divider() => new Border { Height = 1 }.Brush(Border.BackgroundProperty, Keys.Separator);

	public static ComboBox DuePresetBox(DuePreset selected)
	{
		var box = new ComboBox
		{
			MinWidth = 0,
			VerticalAlignment = VerticalAlignment.Center,
			ItemsSource = DuePresets.All.Select(PresetLabel).ToList(),
		};
		box.SelectedIndex = DuePresets.All.ToList().IndexOf(selected);
		return box;
	}

	public static DuePreset SelectedPreset(ComboBox box) =>
		box.SelectedIndex >= 0 && box.SelectedIndex < DuePresets.All.Count ? DuePresets.All[box.SelectedIndex] : DuePreset.None;

	public static string PresetLabel(DuePreset preset) => preset switch
	{
		DuePreset.InOneHour => DesktopText.DueInOneHour,
		DuePreset.TodayEvening => DesktopText.DueTodayEvening,
		DuePreset.TomorrowMorning => DesktopText.DueTomorrowMorning,
		DuePreset.NextWeek => DesktopText.DueNextWeek,
		_ => DesktopText.DueNone,
	};
}
