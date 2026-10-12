using System.Globalization;
using System.Text.RegularExpressions;

namespace TodoList.Core.Input;

/// <summary>
/// Parses a typed due date: empty (no due date), ISO 8601 with offset, a local date and/or time in the
/// invariant <c>yyyy-MM-dd HH:mm</c> form or the culture's short form, a time only (today, or tomorrow when
/// already past), a relative offset such as <c>+30m</c>, <c>+2h</c>, <c>+3d</c>, <c>+1w</c>, or a day word
/// with an optional time in English or German: <c>today</c>, <c>tomorrow 9</c>, <c>fri 18:00</c>,
/// <c>morgen 9 Uhr</c>, <c>next week</c>, <c>20.10. 14:30</c>, <c>6pm</c>.
/// A day without a time means 09:00; <c>today</c> without a time means 18:00.
/// </summary>
public static partial class DueInputParser
{
	private static readonly string[] InvariantDateTimeFormats =
		["yyyy-MM-dd HH:mm", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss"];

	public static bool TryParse(
		string? text,
		CultureInfo culture,
		DateTimeOffset nowUtc,
		TimeZoneInfo zone,
		out DateTimeOffset? dueUtc)
	{
		ArgumentNullException.ThrowIfNull(culture);
		ArgumentNullException.ThrowIfNull(zone);
		dueUtc = null;

		var input = text?.Trim() ?? string.Empty;
		if (input.Length == 0)
		{
			return true;
		}

		if (RelativePattern().Match(input) is { Success: true } relative)
		{
			var amount = int.Parse(relative.Groups["amount"].Value, CultureInfo.InvariantCulture);
			if (amount > 10_000)
			{
				return false;
			}

			dueUtc = relative.Groups["unit"].Value.ToUpperInvariant() switch
			{
				"M" => nowUtc.AddMinutes(amount),
				"H" => nowUtc.AddHours(amount),
				"D" => nowUtc.AddDays(amount),
				_ => nowUtc.AddDays(7 * amount),
			};
			return true;
		}

		// Only accept explicit offsets here; a bare local time must go through the time zone below.
		if (HasExplicitOffset(input) &&
			DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var absolute))
		{
			dueUtc = absolute.ToUniversalTime();
			return true;
		}

		// Recognized but impossible input (13pm, 31.2.) is rejected instead of reaching the lenient parsers.
		if (TryParseNatural(input, nowUtc, zone, out var natural) is { } recognized)
		{
			dueUtc = recognized ? natural : null;
			return recognized;
		}

		if (DateTime.TryParseExact(input, InvariantDateTimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime) ||
			DateTime.TryParseExact(input, ShortDateTimeFormats(culture), culture, DateTimeStyles.None, out dateTime))
		{
			dueUtc = LocalTime.ToUtc(dateTime, zone);
			return true;
		}

		if (DateOnly.TryParseExact(input, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ||
			DateOnly.TryParse(input, culture, DateTimeStyles.None, out date))
		{
			dueUtc = DuePresets.At(date, DuePresets.Morning, zone);
			return true;
		}

		if (TimeOnly.TryParseExact(input, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ||
			TimeOnly.TryParse(input, culture, DateTimeStyles.None, out time))
		{
			var today = DateOnly.FromDateTime(LocalTime.ToWallClock(nowUtc, zone));
			var candidate = DuePresets.At(today, time, zone);
			dueUtc = candidate > nowUtc ? candidate : DuePresets.At(today.AddDays(1), time, zone);
			return true;
		}

		return false;
	}

	private static readonly Dictionary<string, int> DayOffsets = new(StringComparer.OrdinalIgnoreCase)
	{
		["today"] = 0, ["tod"] = 0, ["heute"] = 0,
		["tomorrow"] = 1, ["tmr"] = 1, ["tom"] = 1, ["morgen"] = 1,
		["day after tomorrow"] = 2, ["übermorgen"] = 2, ["uebermorgen"] = 2,
	};

	private static readonly Dictionary<string, DayOfWeek> Weekdays = new(StringComparer.OrdinalIgnoreCase)
	{
		["mon"] = DayOfWeek.Monday, ["monday"] = DayOfWeek.Monday, ["mo"] = DayOfWeek.Monday, ["montag"] = DayOfWeek.Monday,
		["tue"] = DayOfWeek.Tuesday, ["tues"] = DayOfWeek.Tuesday, ["tuesday"] = DayOfWeek.Tuesday, ["di"] = DayOfWeek.Tuesday, ["dienstag"] = DayOfWeek.Tuesday,
		["wed"] = DayOfWeek.Wednesday, ["wednesday"] = DayOfWeek.Wednesday, ["mi"] = DayOfWeek.Wednesday, ["mittwoch"] = DayOfWeek.Wednesday,
		["thu"] = DayOfWeek.Thursday, ["thur"] = DayOfWeek.Thursday, ["thurs"] = DayOfWeek.Thursday, ["thursday"] = DayOfWeek.Thursday,
		["do"] = DayOfWeek.Thursday, ["donnerstag"] = DayOfWeek.Thursday,
		["fri"] = DayOfWeek.Friday, ["friday"] = DayOfWeek.Friday, ["fr"] = DayOfWeek.Friday, ["freitag"] = DayOfWeek.Friday,
		["sat"] = DayOfWeek.Saturday, ["saturday"] = DayOfWeek.Saturday, ["sa"] = DayOfWeek.Saturday, ["samstag"] = DayOfWeek.Saturday,
		["sun"] = DayOfWeek.Sunday, ["sunday"] = DayOfWeek.Sunday, ["so"] = DayOfWeek.Sunday, ["sonntag"] = DayOfWeek.Sunday,
	};

	/// <summary>A day word, weekday or short date, optionally followed by a time; or a time with am, pm or Uhr.</summary>
	private static bool? TryParseNatural(string input, DateTimeOffset nowUtc, TimeZoneInfo zone, out DateTimeOffset due)
	{
		due = default;
		var match = NaturalPattern().Match(input);
		if (!match.Success)
		{
			return null;
		}

		var today = DateOnly.FromDateTime(LocalTime.ToWallClock(nowUtc, zone));
		var dayText = match.Groups["day"].Value.Trim();
		var hasTime = match.Groups["hour"].Success;
		TimeOnly? time = null;
		if (hasTime)
		{
			if (!TryReadTime(match, out var parsedTime))
			{
				return dayText.Length == 0 && match.Groups["suffix"].Value.Length == 0 ? null : false;
			}

			time = parsedTime;
		}

		DateOnly date;
		if (dayText.Length == 0)
		{
			// A bare time with am, pm or Uhr: the next time it comes round.
			if (match.Groups["suffix"].Value.Length == 0)
			{
				return null;
			}

			if (time is not { } bare)
			{
				return false;
			}

			var candidate = DuePresets.At(today, bare, zone);
			due = candidate > nowUtc ? candidate : DuePresets.At(today.AddDays(1), bare, zone);
			return true;
		}

		if (DayOffsets.TryGetValue(dayText, out var offset))
		{
			date = today.AddDays(offset);
			if (offset == 0 && time is null)
			{
				var evening = DuePresets.At(today, DuePresets.Evening, zone);
				due = evening > nowUtc ? evening : nowUtc.AddHours(1);
				return true;
			}
		}
		else if (dayText.Equals("next week", StringComparison.OrdinalIgnoreCase) ||
			dayText.Equals("nächste woche", StringComparison.OrdinalIgnoreCase) ||
			dayText.Equals("naechste woche", StringComparison.OrdinalIgnoreCase))
		{
			var days = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
			date = today.AddDays(days == 0 ? 7 : days);
		}
		else if (Weekdays.TryGetValue(dayText, out var weekday))
		{
			// The coming occurrence; today only when the time has not passed yet.
			var days = ((int)weekday - (int)today.DayOfWeek + 7) % 7;
			date = today.AddDays(days);
			if (days == 0 && DuePresets.At(date, time ?? DuePresets.Morning, zone) <= nowUtc)
			{
				date = date.AddDays(7);
			}
		}
		else if (ShortDatePattern().Match(dayText) is { Success: true } shortDate)
		{
			var day = int.Parse(shortDate.Groups["d"].Value, CultureInfo.InvariantCulture);
			var month = int.Parse(shortDate.Groups["m"].Value, CultureInfo.InvariantCulture);
			var year = shortDate.Groups["y"].Success ? int.Parse(shortDate.Groups["y"].Value, CultureInfo.InvariantCulture) : today.Year;
			if (year < 100)
			{
				year += 2000;
			}

			if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year, month))
			{
				return false;
			}

			date = new DateOnly(year, month, day);
			if (!shortDate.Groups["y"].Success && DuePresets.At(date, time ?? DuePresets.Morning, zone) <= nowUtc)
			{
				date = date.AddYears(1);
			}
		}
		else
		{
			return null;
		}

		due = DuePresets.At(date, time ?? DuePresets.Morning, zone);
		return true;
	}

	private static bool TryReadTime(Match match, out TimeOnly time)
	{
		time = default;
		var hour = int.Parse(match.Groups["hour"].Value, CultureInfo.InvariantCulture);
		var minute = match.Groups["minute"].Success ? int.Parse(match.Groups["minute"].Value, CultureInfo.InvariantCulture) : 0;
		switch (match.Groups["suffix"].Value.ToUpperInvariant())
		{
			case "PM" when hour is >= 1 and < 12:
				hour += 12;
				break;
			case "AM" when hour == 12:
				hour = 0;
				break;
			case "AM" or "PM" when hour is < 1 or > 12:
				return false;
		}

		if (hour > 23 || minute > 59)
		{
			return false;
		}

		time = new TimeOnly(hour, minute);
		return true;
	}

	/// <summary>The invariant text form the parser always accepts, for prefilling an edit field.</summary>
	public static string FormatForEditing(DateTimeOffset dueUtc, TimeZoneInfo zone) =>
		LocalTime.ToWallClock(dueUtc, zone).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

	private static bool HasExplicitOffset(string input) =>
		input.EndsWith('Z') || input.EndsWith('z') || OffsetPattern().IsMatch(input);

	private static string[] ShortDateTimeFormats(CultureInfo culture)
	{
		var format = culture.DateTimeFormat;
		return [$"{format.ShortDatePattern} {format.ShortTimePattern}", $"{format.ShortDatePattern} {format.LongTimePattern}"];
	}

	[GeneratedRegex(@"^(?<day>(?:\d{1,2}\.\d{1,2}\.?(?:\d{2,4})?)|(?:[a-zäöü]+(?: (?!at\b|um\b)[a-zäöü]+)?))?\s*(?:(?:um|at|@)\s*)?(?:(?<hour>\d{1,2})(?:[:.](?<minute>\d{2}))?\s*(?<suffix>am|pm|uhr|h)?)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
	private static partial Regex NaturalPattern();

	[GeneratedRegex(@"^(?<d>\d{1,2})\.(?<m>\d{1,2})\.?(?<y>\d{2,4})?$", RegexOptions.CultureInvariant)]
	private static partial Regex ShortDatePattern();

	[GeneratedRegex(@"^\+\s*(?<amount>\d{1,6})\s*(?<unit>[mhdwMHDW])$", RegexOptions.CultureInvariant)]
	private static partial Regex RelativePattern();

	[GeneratedRegex(@"T\d{2}:\d{2}(:\d{2}(\.\d+)?)?[+-]\d{2}:?\d{2}$", RegexOptions.CultureInvariant)]
	private static partial Regex OffsetPattern();
}
