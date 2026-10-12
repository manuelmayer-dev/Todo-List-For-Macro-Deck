using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using TodoList.Core;
using TodoList.Core.Settings;

namespace TodoList.Plugin.Actions;

/// <summary>
/// Completes the chosen todo, or with no choice the next one: the same todo <c>todo_next_title</c> shows.
/// </summary>
internal sealed class CompleteTodoAction(TodoActionSupport support, SettingsService settings) : IDynamicOptionsActionDefinition
{
	public string Id => "complete-todo";

	public LocalizedText Name => Strings.Actions.Complete.Name();

	public LocalizedText Description => Strings.Actions.Complete.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice(
			TodoActionSupport.TodoIdParameter,
			label: Strings.Actions.Common.Todo(),
			placeholder: Strings.Actions.Complete.NextOpen()),
	];

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken) =>
		Task.FromResult(new DynamicOptionsResult { Options = TodoActionSupport.Options(support.Todos.Snapshot.Open, context.Filter) });

	public IActionExecutor CreateExecutor() => new Executor(support, settings);

	private sealed class Executor(TodoActionSupport support, SettingsService settings) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (await support.EnsureReadyAsync(context.CancellationToken) is { } notReady)
			{
				return notReady;
			}

			Guid id;
			if (string.IsNullOrWhiteSpace(TodoActionSupport.ReadText(context, TodoActionSupport.TodoIdParameter)))
			{
				if (support.Todos.Snapshot.Next(settings.Current.DefaultSort, support.Clock.GetUtcNow(), settings.Current.Thresholds) is not { } next)
				{
					return ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Errors.NoOpenTodo());
				}

				id = next.Id;
			}
			else if (!TodoActionSupport.TryReadId(context, out id))
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, Strings.Errors.InvalidTodo());
			}

			return await support.RunAsync(async () =>
				await support.Todos.CompleteAsync(id, context.CancellationToken) is null
					? ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Errors.TodoNotFound())
					: ActionResult.Success(), "complete-todo");
		}
	}
}
