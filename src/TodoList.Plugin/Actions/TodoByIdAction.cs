using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using TodoList.Core;

namespace TodoList.Plugin.Actions;

/// <summary>
/// An action that applies one store operation to a todo the user picks from a list.
/// </summary>
internal abstract class TodoByIdAction(TodoActionSupport support) : IDynamicOptionsActionDefinition
{
	public abstract string Id { get; }

	public abstract LocalizedText Name { get; }

	public abstract LocalizedText Description { get; }

	public IReadOnlyList<ActionParameter> Parameters { get; } =
	[
		ActionParameter.DynamicChoice(TodoActionSupport.TodoIdParameter, label: Strings.Actions.Common.Todo(), required: true),
	];

	protected TodoActionSupport Support { get; } = support;

	public Task<DynamicOptionsResult> GetDynamicOptionsAsync(DynamicOptionsContext context, CancellationToken cancellationToken) =>
		Task.FromResult(new DynamicOptionsResult { Options = TodoActionSupport.Options(Candidates(Support.Todos.Snapshot), context.Filter) });

	public IActionExecutor CreateExecutor() => new Executor(this);

	protected abstract IEnumerable<TodoItem> Candidates(TodoSnapshot snapshot);

	/// <summary>Returns null when the todo does not exist.</summary>
	protected abstract Task<TodoItem?> ApplyAsync(Guid id, CancellationToken cancellationToken);

	private sealed class Executor(TodoByIdAction action) : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			if (await action.Support.EnsureReadyAsync(context.CancellationToken) is { } notReady)
			{
				return notReady;
			}

			if (string.IsNullOrWhiteSpace(TodoActionSupport.ReadText(context, TodoActionSupport.TodoIdParameter)))
			{
				return ActionResult.Failed(
					ActionErrorCodes.InvalidParameter,
					MacroDeckStrings.Validation.Required(Strings.Actions.Common.Todo()));
			}

			if (!TodoActionSupport.TryReadId(context, out var id))
			{
				return ActionResult.Failed(ActionErrorCodes.InvalidParameter, Strings.Errors.InvalidTodo());
			}

			return await action.Support.RunAsync(async () =>
				await action.ApplyAsync(id, context.CancellationToken) is null
					? ActionResult.Failed(ActionErrorCodes.NotFound, Strings.Errors.TodoNotFound())
					: ActionResult.Success(), action.Id);
		}
	}
}

internal sealed class ReopenTodoAction(TodoActionSupport support) : TodoByIdAction(support)
{
	public override string Id => "reopen-todo";

	public override LocalizedText Name => Strings.Actions.Reopen.Name();

	public override LocalizedText Description => Strings.Actions.Reopen.Description();

	protected override IEnumerable<TodoItem> Candidates(TodoSnapshot snapshot) =>
		snapshot.Completed.OrderByDescending(t => t.CompletedAt);

	protected override Task<TodoItem?> ApplyAsync(Guid id, CancellationToken cancellationToken) =>
		Support.Todos.ReopenAsync(id, cancellationToken);
}

internal sealed class DeleteTodoAction(TodoActionSupport support) : TodoByIdAction(support)
{
	public override string Id => "delete-todo";

	public override LocalizedText Name => Strings.Actions.Delete.Name();

	public override LocalizedText Description => Strings.Actions.Delete.Description();

	protected override IEnumerable<TodoItem> Candidates(TodoSnapshot snapshot) =>
		snapshot.Open.OrderBy(t => t.SortOrder).Concat(snapshot.Completed.OrderByDescending(t => t.CompletedAt));

	protected override Task<TodoItem?> ApplyAsync(Guid id, CancellationToken cancellationToken) =>
		Support.Todos.DeleteAsync(id, cancellationToken);
}
