namespace TodoList.Core.Input;

public static class LocalTime
{
	/// <summary>
	/// Converts a wall-clock time in <paramref name="zone"/> to UTC. A time skipped by a daylight saving
	/// change moves forward by the gap; an ambiguous time resolves to the earlier occurrence.
	/// </summary>
	public static DateTimeOffset ToUtc(DateTime wallClock, TimeZoneInfo zone)
	{
		ArgumentNullException.ThrowIfNull(zone);
		var local = DateTime.SpecifyKind(wallClock, DateTimeKind.Unspecified);

		if (zone.IsInvalidTime(local))
		{
			var gap = zone.GetUtcOffset(local.AddHours(6)) - zone.GetUtcOffset(local.AddHours(-6));
			local = local.Add(gap > TimeSpan.Zero ? gap : TimeSpan.FromHours(1));
		}

		var offset = zone.IsAmbiguousTime(local)
			? zone.GetAmbiguousTimeOffsets(local).Max()
			: zone.GetUtcOffset(local);
		return new DateTimeOffset(local, offset).ToUniversalTime();
	}

	public static DateTime ToWallClock(DateTimeOffset utc, TimeZoneInfo zone) =>
		TimeZoneInfo.ConvertTime(utc, zone).DateTime;
}
