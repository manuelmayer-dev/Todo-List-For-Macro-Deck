using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using TodoList.Plugin.Ui;

namespace TodoList.Plugin.Actions;

/// <summary>
/// Opens the todo list modal on the Macro Deck client that pressed the button. It is also the widget's
/// default short-press action, so tapping a todo widget opens the list.
/// </summary>
internal sealed class OpenTodoListAction : IActionDefinition
{
	public const string ActionId = "open-todo-list";

	public string Id => ActionId;

	public LocalizedText Name => Strings.Actions.OpenList.Name();

	public LocalizedText Description => Strings.Actions.OpenList.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public IActionExecutor CreateExecutor() => new Executor();

	private sealed class Executor : IActionExecutor
	{
		public async Task<ActionResult> ExecuteAsync(ActionExecutionContext context)
		{
			// Automations, scripts and hardware decks have no screen to show a modal on.
			if (context.Ui is null || context.OriginClientId is null)
			{
				return ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.NeedsClient());
			}

			var shown = await context.Ui.ShowModalAsync(
				context.OriginClientId,
				new ModalDefinition { ViewId = TodoModalView.ViewId, Title = Strings.Modal.Title() },
				context.CancellationToken);

			return shown ? ActionResult.Success() : ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.NeedsClient());
		}
	}
}
