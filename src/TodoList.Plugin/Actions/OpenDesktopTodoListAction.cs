using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using TodoList.Desktop;

namespace TodoList.Plugin.Actions;

/// <summary>
/// Opens the tray popup on the computer running Macro Deck.
/// </summary>
internal sealed class OpenDesktopTodoListAction(IDesktopUi desktop) : IActionDefinition
{
	public string Id => "open-desktop-todo-list";

	public LocalizedText Name => Strings.Actions.OpenDesktop.Name();

	public LocalizedText Description => Strings.Actions.OpenDesktop.Description();

	public IReadOnlyList<ActionParameter> Parameters { get; } = [];

	public IActionExecutor CreateExecutor() => new Executor(desktop);

	private sealed class Executor(IDesktopUi desktop) : IActionExecutor
	{
		public Task<ActionResult> ExecuteAsync(ActionExecutionContext context) =>
			desktop.ShowTodoPopup()
				? ActionResult.SucceededTask
				: Task.FromResult(ActionResult.Failed(ActionErrorCodes.Unavailable, Strings.Errors.DesktopUnavailable()));
	}
}
