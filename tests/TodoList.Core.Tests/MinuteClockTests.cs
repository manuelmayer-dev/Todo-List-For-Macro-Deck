using MacroDeck.Plugin.Testing;
using NUnit.Framework;
using TodoList.Core.Scheduling;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class MinuteClockTests
{
	[Test]
	public void Ticks_on_minute_boundaries_only_while_subscribed()
	{
		var time = new ManualTimeProvider(new DateTimeOffset(2026, 1, 1, 10, 0, 40, TimeSpan.Zero));
		using var clock = new MinuteClock(time, TestStore.Logger);
		var ticks = new List<DateTimeOffset>();
		void OnTick(object? sender, DateTimeOffset now) => ticks.Add(now);

		clock.Tick += OnTick;
		time.Advance(TimeSpan.FromSeconds(19));
		time.Advance(TimeSpan.FromSeconds(1));
		time.Advance(TimeSpan.FromMinutes(1));
		clock.Tick -= OnTick;
		time.Advance(TimeSpan.FromMinutes(5));

		Assert.That(ticks, Is.EqualTo(new[]
		{
			new DateTimeOffset(2026, 1, 1, 10, 1, 0, TimeSpan.Zero),
			new DateTimeOffset(2026, 1, 1, 10, 2, 0, TimeSpan.Zero),
		}));
	}
}
