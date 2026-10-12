using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TodoList.Desktop.Theme;

namespace TodoList.Desktop.Views;

/// <summary>
/// The round completion control from Apple's Reminders: an empty ring for open todos (red when overdue)
/// and a filled accent circle with a check mark for completed ones.
/// </summary>
internal static class CheckCircle
{
	public static Border Create(bool completed, bool overdue, Func<Task> toggle)
	{
		var circle = new Border
		{
			Width = 18,
			Height = 18,
			CornerRadius = new CornerRadius(9),
			BorderThickness = new Thickness(1.5),
			VerticalAlignment = VerticalAlignment.Center,
			Cursor = new Cursor(StandardCursorType.Hand),
			Background = Brushes.Transparent,
		};

		if (completed)
		{
			circle.Brush(Border.BackgroundProperty, Keys.Accent).Brush(Border.BorderBrushProperty, Keys.Accent);
			circle.Child = Icons.Create(Icons.Check, 11, Keys.OnAccent);
		}
		else
		{
			circle.Brush(Border.BorderBrushProperty, overdue ? Keys.Red : Keys.LabelTertiary);
		}

		circle.PointerReleased += async (_, e) =>
		{
			e.Handled = true;
			await toggle();
		};
		return circle;
	}
}
