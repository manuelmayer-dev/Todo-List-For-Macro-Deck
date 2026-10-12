using System.Globalization;
using NUnit.Framework;
using TodoList.Core;
using TodoList.Desktop.ViewModels;

namespace TodoList.Desktop.Tests;

[TestFixture]
[SetUICulture("en-GB")]
public sealed class DueFormatterTests
{
	private static readonly TimeZoneInfo Berlin = TimeZoneInfo.FindSystemTimeZoneById("Europe/Berlin");
	private static readonly DateTimeOffset Now = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);
	private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-GB");

	private static string Format(DateTimeOffset? due, TodoStatus status) => DueFormatter.Format(
		new TodoItem { Id = Guid.NewGuid(), Title = "x", CreatedAt = Now, DueAt = due },
		status,
		Now,
		Berlin,
		English);

	[Test]
	public void No_due_date_gives_no_label() => Assert.That(Format(null, TodoStatus.Open), Is.Empty);

	[Test]
	public void Overdue_wins_over_the_time() => Assert.That(Format(Now.AddDays(-2), TodoStatus.Overdue), Is.EqualTo("Overdue"));

	[Test]
	public void Within_the_hour_counts_minutes() => Assert.That(Format(Now.AddMinutes(25), TodoStatus.Due), Is.EqualTo("In 25 min"));

	[Test]
	public void Later_today_and_tomorrow_use_the_local_time()
	{
		Assert.Multiple(() =>
		{
			Assert.That(Format(Now.AddHours(5), TodoStatus.Open), Is.EqualTo("Today 18:00"));
			Assert.That(Format(Now.AddHours(21), TodoStatus.Open), Is.EqualTo("Tomorrow 10:00"));
		});
	}

	[Test]
	public void Later_this_week_shows_the_weekday_and_beyond_only_the_date()
	{
		Assert.Multiple(() =>
		{
			Assert.That(Format(Now.AddDays(3), TodoStatus.Open), Is.EqualTo("Fri 13:00"));
			Assert.That(Format(Now.AddDays(20), TodoStatus.Open), Is.EqualTo("30 March"));
		});
	}

	// Now is Tuesday 13:00 in Berlin.
	[TestCase(0, 18, "Today 18:00")]
	[TestCase(1, 9, "Tomorrow 09:00")]
	[TestCase(3, 18, "Friday, 18:00")]
	[TestCase(10, 14, "20 March, 14:00")]
	public void The_typed_date_preview_spells_out_the_day(int days, int hour, string expected)
	{
		var due = new DateTimeOffset(2026, 3, 10, hour - 1, 0, 0, TimeSpan.Zero).AddDays(days);

		Assert.That(DueFormatter.Describe(due, Now, Berlin, English), Is.EqualTo(expected));
	}
}
