using System.Globalization;
using MacroDeck.Sdk.Variables;
using TodoList.Core;
using TodoList.Core.Settings;

namespace TodoList.Plugin.Variables;

/// <summary>
/// The todo variables. Names are what users type (<c>{{ vars.todo_open_count }}</c>) and ids are persisted in
/// profiles, so neither may change. Eager variables are polled by the host, so values reach Macro Deck within
/// <see cref="RefreshInterval"/>; reads come from the in-memory snapshot and are cheap.
/// </summary>
public sealed class TodoVariables(TodoService todos, SettingsService settings, TimeProvider clock)
{
	public const string OpenCount = "open-count";
	public const string CompletedCount = "completed-count";
	public const string DueCount = "due-count";
	public const string OverdueCount = "overdue-count";
	public const string TotalCount = "total-count";
	public const string NextTitle = "next-title";
	public const string NextDueAt = "next-due-at";
	public const string NextDueInMinutes = "next-due-in-minutes";

	internal static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(5);

	public static IReadOnlyList<VariableDefinition> Definitions { get; } =
	[
		Count("todo_open_count", OpenCount, Strings.Variables.OpenCount()),
		Count("todo_completed_count", CompletedCount, Strings.Variables.CompletedCount()),
		Count("todo_due_count", DueCount, Strings.Variables.DueCount()),
		Count("todo_overdue_count", OverdueCount, Strings.Variables.OverdueCount()),
		Count("todo_total_count", TotalCount, Strings.Variables.TotalCount()),
		VariableDefinition.Eager("todo_next_title", VariableType.Text, refreshInterval: RefreshInterval)
			with { Id = NextTitle, Description = Strings.Variables.NextTitle() },
		VariableDefinition.Eager("todo_next_due_at", VariableType.Text, refreshInterval: RefreshInterval)
			with { Id = NextDueAt, Description = Strings.Variables.NextDueAt() },
		VariableDefinition.Eager("todo_next_due_in_minutes", VariableType.Numeric, decimalPlaces: 0, refreshInterval: RefreshInterval)
			with { Id = NextDueInMinutes, Description = Strings.Variables.NextDueInMinutes(), Unit = "min" },
	];

	/// <summary>
	/// "Next" is the open todo with the earliest deadline, overdue ones included (minutes are then negative).
	/// The title falls back to the first open todo in the default order when no open todo has a due date.
	/// </summary>
	public VariableReading Read(string localId)
	{
		var snapshot = todos.Snapshot;
		var now = clock.GetUtcNow();
		var current = settings.Current;
		var next = snapshot.NextDue();

		return localId switch
		{
			OpenCount or CompletedCount or DueCount or OverdueCount or TotalCount =>
				ReadCount(localId, snapshot.Count(now, current.Thresholds)),
			NextTitle => snapshot.Next(current.DefaultSort, now, current.Thresholds) is { } todo
				? VariableReading.Of(todo.Title)
				: VariableReading.Unavailable,
			NextDueAt => next?.DueAt is { } dueAt
				? VariableReading.Of(dueAt.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture))
				: VariableReading.Unavailable,
			NextDueInMinutes => next?.DueAt is { } dueAt
				? VariableReading.Of((int)Math.Floor((dueAt - now).TotalMinutes))
				: VariableReading.Unavailable,
			_ => VariableReading.Unavailable,
		};
	}

	private static VariableReading ReadCount(string localId, TodoCounts counts) => VariableReading.Of(localId switch
	{
		OpenCount => counts.Open,
		CompletedCount => counts.Completed,
		DueCount => counts.Due,
		OverdueCount => counts.Overdue,
		_ => counts.Total,
	});

	private static VariableDefinition Count(string name, string id, MacroDeck.Localization.LocalizedText description) =>
		VariableDefinition.Eager(name, VariableType.Numeric, decimalPlaces: 0, refreshInterval: RefreshInterval)
			with { Id = id, Description = description };
}
