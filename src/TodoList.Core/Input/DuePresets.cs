namespace TodoList.Core.Input;

public enum DuePreset
{
	None,
	InOneHour,
	TodayEvening,
	TomorrowMorning,
	NextWeek,
}

/// <summary>
/// Quick due-date choices shared by the desktop UI, the Macro Deck modal and the default-due setting.
/// </summary>
public static class DuePresets
{
	public static readonly TimeOnly Morning = new(9, 0);

	public static readonly TimeOnly Evening = new(18, 0);

	public static IReadOnlyList<DuePreset> All { get; } = Enum.GetValues<DuePreset>();

	/// <summary>Resolves a preset at <paramref name="nowUtc"/> in <paramref name="zone"/>; null for None.</summary>
	public static DateTimeOffset? Resolve(DuePreset preset, DateTimeOffset nowUtc, TimeZoneInfo zone)
	{
		var today = DateOnly.FromDateTime(LocalTime.ToWallClock(nowUtc, zone));
		return preset switch
		{
			DuePreset.None => null,
			DuePreset.InOneHour => nowUtc.AddHours(1),
			DuePreset.TodayEvening => At(today, Evening, zone) is var evening && evening > nowUtc
				? evening
				: At(today.AddDays(1), Evening, zone),
			DuePreset.TomorrowMorning => At(today.AddDays(1), Morning, zone),
			DuePreset.NextWeek => At(NextMonday(today), Morning, zone),
			_ => throw new ArgumentOutOfRangeException(nameof(preset), preset, null),
		};
	}

	public static DateTimeOffset At(DateOnly date, TimeOnly time, TimeZoneInfo zone) =>
		LocalTime.ToUtc(date.ToDateTime(time), zone);

	private static DateOnly NextMonday(DateOnly today)
	{
		var days = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
		return today.AddDays(days == 0 ? 7 : days);
	}
}
