using System.Globalization;
using MacroDeck.Ui.Dsl;
using TodoList.Core;
using TodoList.Core.Input;

namespace TodoList.Plugin.Ui;

/// <summary>
/// Shared presentation for widget and modal. Macro Deck UI surfaces carry no culture in this SDK version, so
/// wording is localized per client while times use a neutral 24-hour and ISO date form in this computer's
/// time zone.
/// </summary>
internal static class TodoLabels
{
	public const string OverdueColor = "#ff453a";
	public const string DueColor = "#ff9f0a";
	public const string DoneColor = "#30d158";
	public const string OpenRingColor = "#8e8e93";

	public static UiText Due(TodoItem todo, TodoStatus status, DateTimeOffset now)
	{
		if (todo.DueAt is not { } dueAt)
		{
			return UiText.None();
		}

		if (status == TodoStatus.Overdue)
		{
			return Strings.Due.Overdue();
		}

		var minutes = (int)Math.Ceiling((dueAt - now).TotalMinutes);
		if (status != TodoStatus.Completed && minutes is >= 0 and < 60)
		{
			return Strings.Due.InMinutes(minutes.ToString(CultureInfo.InvariantCulture));
		}

		var zone = TimeZoneInfo.Local;
		var local = LocalTime.ToWallClock(dueAt, zone);
		var today = LocalTime.ToWallClock(now, zone).Date;
		var time = local.ToString("HH:mm", CultureInfo.InvariantCulture);
		if (local.Date == today)
		{
			return Strings.Due.TodayAt(time);
		}

		if (local.Date == today.AddDays(1))
		{
			return Strings.Due.TomorrowAt(time);
		}

		return Strings.Due.OnDate(local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
	}

	/// <summary>Color for the due label: semantic for due and overdue, otherwise the theme's muted text.</summary>
	public static string? DueColorFor(TodoStatus status) => status switch
	{
		TodoStatus.Overdue => OverdueColor,
		TodoStatus.Due => DueColor,
		_ => null,
	};
}
