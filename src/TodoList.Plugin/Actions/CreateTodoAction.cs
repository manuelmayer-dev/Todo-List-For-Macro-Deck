using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using TodoList.Core;

namespace TodoList.Plugin.Actions;

/// <summary>
/// Creates a todo. The host renders variables in title, description and due date before the executor runs.
/// </summary>
internal sealed class CreateTodoAction(TodoActionSupport support) : IActionDefinition
{
	private const string TitleParameter = "title";
	private const string DescriptionParameter = "description";
	private const string DueAtParameter = "dueAt";

	private const string ActionId = "create-todo";

	public string Id => ActionId;

	public LocalizedText Name => Strings.Actions.Create.Name();

	public LocalizedText Description => Strings.Actions.Create.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.Text(
			TitleParameter,
			label: Strings.Actions.Create.Title.Label(),
			placeholder: Strings.Actions.Create.Title.Placeholder(),
			required: true,
			maxLength: TodoValidation.MaxTitleLength),
		ActionParameter.MultilineText(
			DescriptionParameter,
			label: Strings.Actions.Create.Notes.Label(),
			maxLength: TodoValidation.MaxDescriptionLength),
		ActionParameter.DateTime(
			DueAtParameter,
			label: Strings.Actions.Create.DueAt.Label(),
			description: Strings.Actions.Create.DueAt.Description()),
	];

	public IActionExecutor CreateExecutor() => new Executor(support);

	private sealed class Executor(TodoActionSupport support) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (await support.EnsureReadyAsync(context.CancellationToken) is { } notReady)
			{
				return notReady;
			}

			if (!support.TryReadDue(context, DueAtParameter, out var dueAt))
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, Strings.Errors.InvalidDueDate());
			}

			var draft = new TodoDraft(
				TodoActionSupport.ReadText(context, TitleParameter) ?? string.Empty,
				TodoActionSupport.ReadText(context, DescriptionParameter),
				dueAt);

			return await support.RunAsync(async () =>
			{
				await support.Todos.CreateAsync(draft, context.CancellationToken);
				return ActionResult.Success();
			}, ActionId);
		}
	}
}
