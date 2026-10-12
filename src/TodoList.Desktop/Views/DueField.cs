using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Calendar = Avalonia.Controls.Calendar;
using TodoList.Core.Input;
using TodoList.Desktop.Localization;
using TodoList.Desktop.Theme;
using TodoList.Desktop.ViewModels;

namespace TodoList.Desktop.Views;

/// <summary>
/// A due date field made for the keyboard: type it in plain words (<c>tomorrow 9</c>, <c>fri 18:00</c>,
/// <c>+2h</c>, <c>20.10.</c>), or step through suggestions with the arrow keys. A live preview on the right
/// says what it means. The calendar button is the mouse way and stays out of the tab order.
/// </summary>
internal sealed class DueField : TextBox
{
	// Parseable in any culture, in the order the arrow keys walk through them.
	private static readonly string[] Suggestions = ["", "+1h", "today 18:00", "tomorrow 9:00", "next week"];

	private readonly TextBlock _preview = Ui.Text(string.Empty, Ui.Small, FontWeight.Medium, Keys.LabelSecondary);
	private readonly Calendar _calendar = CreateCalendar();
	private readonly Flyout _flyout;
	private int _suggestion = -1;
	private bool _settingText;

	public DueField()
	{
		PlaceholderText = DesktopText.DueFieldPlaceholder;
		_preview.Margin = new Thickness(8, 0, 2, 0);

		_flyout = new Flyout { Content = _calendar, Placement = PlacementMode.BottomEdgeAlignedLeft };
		PickButton = new Button
		{
			Content = Icons.Create(Icons.Calendar, 14, Keys.LabelSecondary),
			Focusable = false,
			Flyout = _flyout,
			Margin = new Thickness(0, 0, 6, 0),
			Cursor = new Cursor(StandardCursorType.Hand),
		};
		PickButton.Classes.Add("plain");
		PickButton.Classes.Add("icon");
		PickButton.Width = 24;
		PickButton.Height = 24;
		ToolTip.SetTip(PickButton, DesktopText.PickDate);
		_flyout.Opening += (_, _) => ShowInCalendar();
		_calendar.SelectedDatesChanged += (_, _) => PickFromCalendar();

		InnerLeftContent = PickButton;
		InnerRightContent = _preview;
		VerticalContentAlignment = VerticalAlignment.Center;
		TextChanged += (_, _) => OnTextChanged();
		AddHandler(KeyDownEvent, OnArrowKey, RoutingStrategies.Tunnel);
		UpdatePreview();
	}

	protected override Type StyleKeyOverride => typeof(TextBox);

	/// <summary>The calendar button, exposed so a layout can align it.</summary>
	public Button PickButton { get; }

	/// <summary>
	/// A calendar sized to its smaller day circles. Fluent's template pins the month grid to the width of its
	/// own 40 pixel cells as a template value, which a style cannot override.
	/// </summary>
	internal static Calendar CreateCalendar()
	{
		var calendar = new Calendar { SelectionMode = CalendarSelectionMode.SingleDate, IsTodayHighlighted = true };
		calendar.Loaded += (_, _) =>
		{
			foreach (var grid in calendar.GetVisualDescendants().OfType<Grid>().Where(g => g.MinWidth > 0))
			{
				grid.MinWidth = 0;
			}
		};
		return calendar;
	}

	/// <summary>Raised whenever the meaning of the field changes.</summary>
	public event EventHandler? DueChanged;

	/// <summary>The field's date, or null without one; false while the text is not understood.</summary>
	public bool TryGetDue(out DateTimeOffset? dueAt) =>
		DueInputParser.TryParse(Text, CultureInfo.CurrentCulture, DateTimeOffset.UtcNow, TimeZoneInfo.Local, out dueAt);

	public void SetDue(DateTimeOffset? dueAt) =>
		SetText(dueAt is { } value ? DueInputParser.FormatForEditing(value, TimeZoneInfo.Local) : string.Empty);

	public void SetPreset(DuePreset preset) => SetText(preset switch
	{
		DuePreset.InOneHour => Suggestions[1],
		DuePreset.TodayEvening => Suggestions[2],
		DuePreset.TomorrowMorning => Suggestions[3],
		DuePreset.NextWeek => Suggestions[4],
		_ => Suggestions[0],
	});

	private void SetText(string text)
	{
		_settingText = true;
		Text = text;
		CaretIndex = text.Length;
		_settingText = false;
		UpdatePreview();
	}

	private void OnTextChanged()
	{
		if (!_settingText)
		{
			_suggestion = -1;
		}

		UpdatePreview();
	}

	private void OnArrowKey(object? sender, KeyEventArgs e)
	{
		if (e.KeyModifiers != KeyModifiers.None || e.Key is not (Key.Up or Key.Down))
		{
			return;
		}

		e.Handled = true;
		var current = _suggestion >= 0 ? _suggestion : Array.IndexOf(Suggestions, (Text ?? string.Empty).Trim());
		var step = e.Key == Key.Down ? 1 : -1;
		_suggestion = current < 0
			? (step > 0 ? 1 : Suggestions.Length - 1)
			: (current + step + Suggestions.Length) % Suggestions.Length;
		SetText(Suggestions[_suggestion]);
	}

	private void UpdatePreview()
	{
		var valid = TryGetDue(out var dueAt);
		_preview.Text = !valid
			? DesktopText.DueUnknown
			: dueAt is { } value
				? DueFormatter.Describe(value, DateTimeOffset.UtcNow, TimeZoneInfo.Local, CultureInfo.CurrentCulture)
				: (Text ?? string.Empty).Length == 0 ? string.Empty : DesktopText.NoDue;
		_preview.Brush(TextBlock.ForegroundProperty, valid ? Keys.LabelSecondary : Keys.Red);
		DueChanged?.Invoke(this, EventArgs.Empty);
	}

	private void ShowInCalendar()
	{
		var date = TryGetDue(out var dueAt) && dueAt is { } value
			? LocalTime.ToWallClock(value, TimeZoneInfo.Local).Date
			: DateTime.Today;
		_settingText = true;
		_calendar.SelectedDate = date;
		_calendar.DisplayDate = date;
		_settingText = false;
	}

	// Keeps the typed time, or 09:00 like a typed date without one.
	private void PickFromCalendar()
	{
		if (_settingText || _calendar.SelectedDate is not { } date)
		{
			return;
		}

		var time = TryGetDue(out var dueAt) && dueAt is { } value
			? TimeOnly.FromDateTime(LocalTime.ToWallClock(value, TimeZoneInfo.Local))
			: DuePresets.Morning;
		SetDue(DuePresets.At(DateOnly.FromDateTime(date), time, TimeZoneInfo.Local));
		_flyout.Hide();
		Focus();
	}
}
