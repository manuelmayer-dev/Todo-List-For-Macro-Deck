using MacroDeck.Localization;
using Serilog;
using TodoList.Core;
using TodoList.Core.Storage;
using TodoList.Plugin.Actions;

namespace TodoList.Plugin.Ui;

public sealed class TodoCommands(TodoService todos, ILogger logger) : ITodoCommands
{
	private readonly ILogger _logger = logger.ForContext<TodoCommands>();

	public Task<LocalizedText?> CreateAsync(TodoDraft draft, CancellationToken cancellationToken) =>
		RunAsync(async () => (await todos.CreateAsync(draft, cancellationToken)) is not null, "create");

	public Task<LocalizedText?> UpdateAsync(Guid id, TodoDraft draft, CancellationToken cancellationToken) =>
		RunAsync(async () => await todos.UpdateAsync(id, draft, cancellationToken) is not null, "update");

	public Task<LocalizedText?> SetCompletedAsync(Guid id, bool completed, CancellationToken cancellationToken) =>
		RunAsync(async () => (completed
			? await todos.CompleteAsync(id, cancellationToken)
			: await todos.ReopenAsync(id, cancellationToken)) is not null, completed ? "complete" : "reopen");

	public Task<LocalizedText?> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
		RunAsync(async () => await todos.DeleteAsync(id, cancellationToken) is not null, "delete");

	private async Task<LocalizedText?> RunAsync(Func<Task<bool>> operation, string name)
	{
		try
		{
			return await operation().ConfigureAwait(false) ? null : Strings.Errors.TodoNotFound();
		}
		catch (TodoValidationException exception)
		{
			return TodoActionSupport.ValidationMessage(exception.Error);
		}
		catch (TodoStoreException exception)
		{
			_logger.Error(exception, "Could not {Operation} a todo from a Macro Deck view", name);
			return Strings.Errors.StoreFailed();
		}
	}
}
