using System.Globalization;
using TodoList.Core;
using TodoList.Core.Input;
using TodoList.Desktop.Localization;

namespace TodoList.Desktop.ViewModels;

internal static class DueFormatter
{
	/// <summary>A short due label in the local time zone and the current culture; empty without a due date.</summary>
	public static string Format(TodoItem todo, TodoStatus status, DateTimeOffset now, TimeZoneInfo zone, CultureInfo culture)
	{
		if (todo.DueAt is not { } dueAt)
		{
			return string.Empty;
		}

		var local = LocalTime.ToWallClock(dueAt, zone);
		var time = local.ToString("t", culture);
		if (status == TodoStatus.Overdue)
		{
			return DesktopText.Overdue;
		}

		var minutes = (int)Math.Ceiling((dueAt - now).TotalMinutes);
		if (status != TodoStatus.Completed && minutes is >= 0 and < 60)
		{
			return DesktopText.DueInMinutes(minutes);
		}

		var today = LocalTime.ToWallClock(now, zone).Date;
		if (local.Date == today)
		{
			return DesktopText.DueToday(time);
		}

		if (local.Date == today.AddDays(1))
		{
			return DesktopText.DueTomorrow(time);
		}

		// Within the coming week the weekday says enough, like Apple's Reminders; later only the date.
		return local.Date > today && local.Date < today.AddDays(7)
			? $"{local.ToString("ddd", culture)} {time}"
			: local.ToString(culture.DateTimeFormat.MonthDayPattern, culture);
	}

	/// <summary>
	/// Spells out a typed due date for the live preview: today and tomorrow by name, the weekday within the
	/// coming week, otherwise the date; always with the time.
	/// </summary>
	public static string Describe(DateTimeOffset dueAt, DateTimeOffset now, TimeZoneInfo zone, CultureInfo culture)
	{
		var local = LocalTime.ToWallClock(dueAt, zone);
		var time = local.ToString("t", culture);
		var today = LocalTime.ToWallClock(now, zone).Date;
		if (local.Date == today)
		{
			return DesktopText.DueToday(time);
		}

		if (local.Date == today.AddDays(1))
		{
			return DesktopText.DueTomorrow(time);
		}

		var date = local.Date > today && local.Date < today.AddDays(7)
			? local.ToString("dddd", culture)
			: local.ToString(local.Year == today.Year ? culture.DateTimeFormat.MonthDayPattern : "d", culture);
		return $"{date}, {time}";
	}
}
