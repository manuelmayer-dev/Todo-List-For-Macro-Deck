using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using TodoList.Desktop.Theme;

namespace TodoList.Desktop.Views;

/// <summary>
/// A macOS-style switch: a pill track in the accent colour when on, with a white knob that slides over.
/// </summary>
internal sealed class AppleSwitch : ToggleButton
{
	private const double TrackWidth = 38;
	private const double TrackHeight = 22;
	private const double Inset = 2;
	private const double KnobSize = TrackHeight - (2 * Inset);

	private readonly Border _knob = new()
	{
		Width = KnobSize,
		Height = KnobSize,
		CornerRadius = new CornerRadius(KnobSize / 2),
		Background = Brushes.White,
		BoxShadow = BoxShadows.Parse("0 1 3 0 #40000000"),
		HorizontalAlignment = HorizontalAlignment.Left,
		VerticalAlignment = VerticalAlignment.Center,
		Margin = new Thickness(Inset),
		Transitions = [new ThicknessTransition { Property = MarginProperty, Duration = TimeSpan.FromMilliseconds(160), Easing = new Avalonia.Animation.Easings.CubicEaseOut() }],
	};

	private readonly Border _track;

	public AppleSwitch()
	{
		_track = new Border
		{
			Width = TrackWidth,
			Height = TrackHeight,
			CornerRadius = new CornerRadius(TrackHeight / 2),
			Child = _knob,
		};
		Template = new FuncControlTemplate<AppleSwitch>((_, _) => _track);
		Cursor = new Cursor(StandardCursorType.Hand);
		Focusable = true;
		Update();
	}

	protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
	{
		base.OnPropertyChanged(change);
		if (change.Property == IsCheckedProperty)
		{
			Update();
		}
	}

	private void Update()
	{
		var on = IsChecked == true;
		_knob.Margin = on ? new Thickness(TrackWidth - KnobSize - Inset, Inset, Inset, Inset) : new Thickness(Inset);
		_track.Brush(Border.BackgroundProperty, on ? Keys.Accent : Keys.SwitchOff);
	}
}
