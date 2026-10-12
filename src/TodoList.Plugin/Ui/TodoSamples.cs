using TodoList.Core;
using TodoList.Core.Settings;

namespace TodoList.Plugin.Ui;

/// <summary>
/// Representative todos for the widget picker and developer previews, where no live data may be read.
/// The titles are sample content, not interface text.
/// </summary>
internal static class TodoSamples
{
	public static readonly DateTimeOffset Now = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

	public static TodoUiState State(int count = 5, bool withOverdue = true, string? longTitle = null)
	{
		string[] titles =
		[
			longTitle ?? "Send the quarterly report",
			"Book dentist appointment",
			"Water the plants",
			"Renew domain",
			"Call the landlord",
			"Order printer ink",
			"Plan team offsite",
			"Back up laptop",
			"Return library books",
		];

		var items = titles.Take(count).Select((title, index) => new TodoItem
		{
			Id = new Guid(index + 1, 0, 0, new byte[8]),
			Title = title,
			CreatedAt = Now.AddDays(-index - 1),
			SortOrder = index,
			DueAt = index switch
			{
				0 when withOverdue => Now.AddHours(-2),
				1 => Now.AddMinutes(25),
				2 => Now.AddHours(6),
				4 => Now.AddDays(3),
				_ => null,
			},
		});

		return new TodoUiState(new TodoSnapshot(items), new TodoSettings(), Now, TodoStoreStatus.Ready);
	}
}
