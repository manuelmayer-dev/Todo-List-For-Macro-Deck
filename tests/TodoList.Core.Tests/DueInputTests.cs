using System.Globalization;
using NUnit.Framework;
using TodoList.Core.Input;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class DueInputTests
{
	private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");

	// 2026-03-10 13:00 local in Berlin (UTC+1).
	private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

	private static DateTimeOffset? Parse(string text, string culture = "en-US")
	{
		Assert.That(DueInputParser.TryParse(text, CultureInfo.GetCultureInfo(culture), Now, Berlin, out var due), Is.True, text);
		return due;
	}

	[Test]
	public void Empty_input_means_no_due_date() => Assert.That(Parse("  "), Is.Null);

	[Test]
	public void Invariant_local_date_and_time_is_converted_to_utc() =>
		Assert.That(Parse("2026-03-12 18:30"), Is.EqualTo(new DateTimeOffset(2026, 3, 12, 17, 30, 0, TimeSpan.Zero)));

	[Test]
	public void An_explicit_offset_is_respected() =>
		Assert.That(Parse("2026-03-12T18:30:00+05:00"), Is.EqualTo(new DateTimeOffset(2026, 3, 12, 13, 30, 0, TimeSpan.Zero)));

	[Test]
	public void A_date_alone_means_nine_in_the_morning() =>
		Assert.That(Parse("2026-03-12"), Is.EqualTo(new DateTimeOffset(2026, 3, 12, 8, 0, 0, TimeSpan.Zero)));

	[Test]
	public void The_culture_short_format_is_accepted() =>
		Assert.That(Parse("12.03.2026 18:30", "de-DE"), Is.EqualTo(new DateTimeOffset(2026, 3, 12, 17, 30, 0, TimeSpan.Zero)));

	[Test]
	public void A_time_that_already_passed_today_means_tomorrow()
	{
		Assert.Multiple(() =>
		{
			Assert.That(Parse("15:00"), Is.EqualTo(new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.Zero)));
			Assert.That(Parse("08:00"), Is.EqualTo(new DateTimeOffset(2026, 3, 11, 7, 0, 0, TimeSpan.Zero)));
		});
	}

	[TestCase("+30m", 30)]
	[TestCase("+2h", 120)]
	[TestCase("+1d", 1440)]
	[TestCase("+1w", 10080)]
	public void Relative_offsets_count_from_now(string text, int minutes) =>
		Assert.That(Parse(text), Is.EqualTo(Now.AddMinutes(minutes)));

	[TestCase("tomorrowish")]
	[TestCase("2026-13-40")]
	[TestCase("+99999999d")]
	public void Garbage_is_rejected(string text) =>
		Assert.That(DueInputParser.TryParse(text, CultureInfo.InvariantCulture, Now, Berlin, out _), Is.False);

	[Test]
	public void A_time_skipped_by_daylight_saving_moves_forward()
	{
		// 2026-03-29 02:30 does not exist in Berlin; clocks jump from 02:00 to 03:00.
		var utc = LocalTime.ToUtc(new DateTime(2026, 3, 29, 2, 30, 0), Berlin);

		Assert.That(utc, Is.EqualTo(new DateTimeOffset(2026, 3, 29, 1, 30, 0, TimeSpan.Zero)));
	}

	[Test]
	public void An_ambiguous_time_resolves_to_the_first_occurrence()
	{
		// 2026-10-25 02:30 happens twice in Berlin; the first one is still on summer time (UTC+2).
		var utc = LocalTime.ToUtc(new DateTime(2026, 10, 25, 2, 30, 0), Berlin);

		Assert.That(utc, Is.EqualTo(new DateTimeOffset(2026, 10, 25, 0, 30, 0, TimeSpan.Zero)));
	}

	[Test]
	public void Tomorrow_morning_keeps_nine_oclock_across_a_daylight_saving_change()
	{
		var saturday = new DateTimeOffset(2026, 3, 28, 12, 0, 0, TimeSpan.Zero);

		var due = DuePresets.Resolve(DuePreset.TomorrowMorning, saturday, Berlin);

		Assert.That(due, Is.EqualTo(new DateTimeOffset(2026, 3, 29, 7, 0, 0, TimeSpan.Zero)));
	}

	[Test]
	public void This_evening_rolls_to_tomorrow_once_it_has_passed()
	{
		var lateEvening = new DateTimeOffset(2026, 3, 10, 20, 0, 0, TimeSpan.Zero);

		Assert.That(DuePresets.Resolve(DuePreset.TodayEvening, lateEvening, Berlin), Is.EqualTo(new DateTimeOffset(2026, 3, 11, 17, 0, 0, TimeSpan.Zero)));
	}

	[Test]
	public void Next_week_is_the_following_monday_morning() =>
		Assert.That(DuePresets.Resolve(DuePreset.NextWeek, Now, Berlin), Is.EqualTo(new DateTimeOffset(2026, 3, 16, 8, 0, 0, TimeSpan.Zero)));

	[Test]
	public void The_editing_format_round_trips()
	{
		var due = new DateTimeOffset(2026, 7, 1, 6, 45, 0, TimeSpan.Zero);

		var text = DueInputParser.FormatForEditing(due, Berlin);

		Assert.That(text, Is.EqualTo("2026-07-01 08:45"));
		Assert.That(Parse(text), Is.EqualTo(due));
	}

	// Now is Tuesday 2026-03-10 13:00 in Berlin (UTC+1, UTC+2 from April), so 09:00 local is 08:00 UTC.
	[TestCase("today", 2026, 3, 10, 17, 0)]
	[TestCase("heute", 2026, 3, 10, 17, 0)]
	[TestCase("tomorrow", 2026, 3, 11, 8, 0)]
	[TestCase("morgen 14:30", 2026, 3, 11, 13, 30)]
	[TestCase("Morgen 9 Uhr", 2026, 3, 11, 8, 0)]
	[TestCase("tomorrow 9am", 2026, 3, 11, 8, 0)]
	[TestCase("tomorrow at 6pm", 2026, 3, 11, 17, 0)]
	[TestCase("übermorgen", 2026, 3, 12, 8, 0)]
	[TestCase("fri 18:00", 2026, 3, 13, 17, 0)]
	[TestCase("Freitag", 2026, 3, 13, 8, 0)]
	[TestCase("mo", 2026, 3, 16, 8, 0)]
	[TestCase("tue 15:00", 2026, 3, 10, 14, 0)]
	[TestCase("tue 9:00", 2026, 3, 17, 8, 0)]
	[TestCase("next week", 2026, 3, 16, 8, 0)]
	[TestCase("nächste Woche", 2026, 3, 16, 8, 0)]
	[TestCase("20.10. 14:30", 2026, 10, 20, 12, 30)]
	[TestCase("1.3.", 2027, 3, 1, 8, 0)]
	[TestCase("6pm", 2026, 3, 10, 17, 0)]
	[TestCase("10am", 2026, 3, 11, 9, 0)]
	[TestCase("17 uhr", 2026, 3, 10, 16, 0)]
	public void Natural_language_is_understood(string text, int year, int month, int day, int hour, int minute) =>
		Assert.That(Parse(text), Is.EqualTo(new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.Zero)));

	[Test]
	public void Today_after_six_means_in_an_hour()
	{
		var late = new DateTimeOffset(2026, 3, 10, 19, 0, 0, TimeSpan.Zero);

		Assert.That(DueInputParser.TryParse("today", CultureInfo.InvariantCulture, late, Berlin, out var due), Is.True);
		Assert.That(due, Is.EqualTo(late.AddHours(1)));
	}

	[TestCase("someday")]
	[TestCase("fri 25:00")]
	[TestCase("13pm")]
	[TestCase("31.2.")]
	public void Unknown_words_and_impossible_times_are_rejected(string text) =>
		Assert.That(DueInputParser.TryParse(text, CultureInfo.InvariantCulture, Now, Berlin, out _), Is.False);
}
