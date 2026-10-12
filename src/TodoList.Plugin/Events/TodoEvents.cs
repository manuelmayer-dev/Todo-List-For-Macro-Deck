using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Events;

namespace TodoList.Plugin.Events;

/// <summary>
/// Event ids and payload keys are persisted in users' triggers; never rename them.
/// </summary>
internal static class TodoEvents
{
	public const string Created = "todo-created";
	public const string Updated = "todo-updated";
	public const string Completed = "todo-completed";
	public const string Reopened = "todo-reopened";
	public const string Deleted = "todo-deleted";
	public const string Due = "todo-due";
	public const string Overdue = "todo-overdue";

	public const string TodoIdKey = "todoId";
	public const string TitleKey = "title";
	public const string DescriptionKey = "description";
	public const string DueAtKey = "dueAt";
	public const string StatusKey = "status";

	public static IReadOnlyList<EventDefinition> Definitions { get; } =
	[
		Define(Created, Strings.Events.Created.Name(), Strings.Events.Created.Description()),
		Define(Updated, Strings.Events.Updated.Name(), Strings.Events.Updated.Description()),
		Define(Completed, Strings.Events.Completed.Name(), Strings.Events.Completed.Description()),
		Define(Reopened, Strings.Events.Reopened.Name(), Strings.Events.Reopened.Description()),
		Define(Deleted, Strings.Events.Deleted.Name(), Strings.Events.Deleted.Description()),
		Define(Due, Strings.Events.Due.Name(), Strings.Events.Due.Description()),
		Define(Overdue, Strings.Events.Overdue.Name(), Strings.Events.Overdue.Description()),
	];

	private static EventDefinition Define(string id, LocalizedText name, LocalizedText description) => new()
	{
		Id = id,
		Name = name,
		Description = description,
		Category = Strings.Events.Category(),
		PayloadParameters =
		[
			ActionParameter.Text(TodoIdKey, Strings.Events.Payload.TodoId()),
			ActionParameter.Text(TitleKey, Strings.Events.Payload.Title()),
			ActionParameter.Text(DescriptionKey, Strings.Events.Payload.Description()),
			ActionParameter.DateTime(DueAtKey, Strings.Events.Payload.DueAt()),
			ActionParameter.Text(StatusKey, Strings.Events.Payload.Status()),
		],
	};
}
