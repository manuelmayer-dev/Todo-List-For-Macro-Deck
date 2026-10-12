using System.Text.Json;
using TodoList.Core;

namespace TodoList.Plugin.Ui;

/// <summary>
/// Per-widget settings. The keys are persisted in users' widget data; never rename them.
/// </summary>
public sealed record TodoWidgetSettings
{
	public const string MaxVisibleKey = "maxVisible";
	public const string SortKey = "sort";
	public const string ShowCompletedKey = "showCompleted";
	public const string ShowDueDatesKey = "showDueDates";
	public const string OverdueFirstKey = "overdueFirst";
	public const string StatusFilterKey = "statusFilter";
	public const string InteractiveKey = "interactive";

	public const int MinVisible = 1;
	public const int MaxVisibleLimit = 8;

	public static TodoWidgetSettings Default { get; } = new();

	public int MaxVisible { get; init; } = 4;

	public TodoSortOrder Sort { get; init; } = TodoSortOrder.DueDate;

	public bool ShowCompleted { get; init; }

	public bool ShowDueDates { get; init; } = true;

	public bool OverdueFirst { get; init; } = true;

	public TodoStatusFilter StatusFilter { get; init; } = TodoStatusFilter.All;

	/// <summary>
	/// Large widgets show complete buttons. Any control in a widget stops a tap on the widget from opening
	/// the list, so this can be turned off to keep the tap.
	/// </summary>
	public bool Interactive { get; init; } = true;

	public static string DefaultDataJson { get; } = ToJson(Default);

	/// <summary>The JSON Schema the host validates saved widget data against.</summary>
	public static string DataSchemaJson { get; } = $$"""
		{
		  "type": "object",
		  "properties": {
		    "{{MaxVisibleKey}}": { "type": "number", "minimum": {{MinVisible}}, "maximum": {{MaxVisibleLimit}} },
		    "{{SortKey}}": { "type": "string", "enum": ["due", "created", "manual"] },
		    "{{ShowCompletedKey}}": { "type": "boolean" },
		    "{{ShowDueDatesKey}}": { "type": "boolean" },
		    "{{OverdueFirstKey}}": { "type": "boolean" },
		    "{{StatusFilterKey}}": { "type": "string", "enum": ["all", "due", "overdue", "due-or-overdue"] },
		    "{{InteractiveKey}}": { "type": "boolean" }
		  }
		}
		""";

	/// <summary>Reads saved widget data; missing or invalid values fall back to the defaults.</summary>
	public static TodoWidgetSettings Read(JsonElement data)
	{
		if (data.ValueKind != JsonValueKind.Object)
		{
			return Default;
		}

		return new TodoWidgetSettings
		{
			MaxVisible = data.TryGetProperty(MaxVisibleKey, out var max) && max.TryGetDouble(out var number)
				? Math.Clamp((int)Math.Round(number), MinVisible, MaxVisibleLimit)
				: Default.MaxVisible,
			Sort = ReadString(data, SortKey) switch
			{
				"created" => TodoSortOrder.CreatedDate,
				"manual" => TodoSortOrder.Manual,
				_ => Default.Sort,
			},
			ShowCompleted = ReadBool(data, ShowCompletedKey) ?? Default.ShowCompleted,
			ShowDueDates = ReadBool(data, ShowDueDatesKey) ?? Default.ShowDueDates,
			OverdueFirst = ReadBool(data, OverdueFirstKey) ?? Default.OverdueFirst,
			StatusFilter = ReadString(data, StatusFilterKey) switch
			{
				"due" => TodoStatusFilter.Due,
				"overdue" => TodoStatusFilter.Overdue,
				"due-or-overdue" => TodoStatusFilter.DueOrOverdue,
				_ => Default.StatusFilter,
			},
			Interactive = ReadBool(data, InteractiveKey) ?? Default.Interactive,
		};
	}

	public static string SortValue(TodoSortOrder sort) => sort switch
	{
		TodoSortOrder.CreatedDate => "created",
		TodoSortOrder.Manual => "manual",
		_ => "due",
	};

	public static string FilterValue(TodoStatusFilter filter) => filter switch
	{
		TodoStatusFilter.Due => "due",
		TodoStatusFilter.Overdue => "overdue",
		TodoStatusFilter.DueOrOverdue => "due-or-overdue",
		_ => "all",
	};

	private static string ToJson(TodoWidgetSettings settings) => JsonSerializer.Serialize(new Dictionary<string, object>
	{
		[MaxVisibleKey] = settings.MaxVisible,
		[SortKey] = SortValue(settings.Sort),
		[ShowCompletedKey] = settings.ShowCompleted,
		[ShowDueDatesKey] = settings.ShowDueDates,
		[OverdueFirstKey] = settings.OverdueFirst,
		[StatusFilterKey] = FilterValue(settings.StatusFilter),
		[InteractiveKey] = settings.Interactive,
	});

	private static string? ReadString(JsonElement data, string key) =>
		data.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

	private static bool? ReadBool(JsonElement data, string key) =>
		data.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
			? value.GetBoolean()
			: null;
}
