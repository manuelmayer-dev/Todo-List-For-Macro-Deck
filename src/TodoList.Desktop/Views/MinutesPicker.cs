using System.Globalization;
using Avalonia.Controls;
using TodoList.Core.Settings;
using TodoList.Desktop.Localization;

namespace TodoList.Desktop.Views;

/// <summary>
/// A pop-up of common durations instead of a numeric spinner, the way macOS settings offer time choices.
/// A stored value that is not in the list is kept as an extra entry.
/// </summary>
internal sealed class MinutesPicker : ComboBox
{
	private static readonly int[] Common = [0, 5, 10, 15, 30, 60, 120, 240, 1440];

	private int[] _values = Common;

	public MinutesPicker()
	{
		MinWidth = 170;
		Minutes = 0;
	}

	protected override Type StyleKeyOverride => typeof(ComboBox);

	public int Minutes
	{
		get => SelectedIndex >= 0 && SelectedIndex < _values.Length ? _values[SelectedIndex] : 0;
		set
		{
			var clamped = Math.Clamp(value, 0, TodoSettings.MaxThresholdMinutes);
			_values = Common.Contains(clamped) ? Common : [.. Common.Append(clamped).Order()];
			ItemsSource = _values.Select(Label).ToList();
			SelectedIndex = Array.IndexOf(_values, clamped);
		}
	}

	internal static string Label(int minutes) => minutes switch
	{
		0 => DesktopText.MinutesNone,
		< 60 => DesktopText.MinutesOption(minutes),
		_ when minutes % 1440 == 0 => DesktopText.DaysOption(minutes / 1440),
		_ when minutes % 60 == 0 => DesktopText.HoursOption(minutes / 60),
		_ => DesktopText.MinutesOption(minutes),
	};
}
