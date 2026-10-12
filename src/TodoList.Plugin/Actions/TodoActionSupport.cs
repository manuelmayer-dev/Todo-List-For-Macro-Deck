using System.Globalization;
using MacroDeck.Localization;
using MacroDeck.Sdk.Actions;
using Serilog;
using TodoList.Core;
using TodoList.Core.Input;
using TodoList.Core.Storage;
using TodoList.Plugin.Hosting;

namespace TodoList.Plugin.Actions;

/// <summary>
/// What every todo action needs: the shared store, a readiness check and the mapping from store and
/// validation failures to truthful, localized results.
/// </summary>
public sealed class TodoActionSupport(TodoService todos, TodoRuntime runtime, TimeProvider clock, ILogger logger)
{
	public const string TodoIdParameter = "todoId";

	private readonly ILogger _logger = logger.ForContext<TodoActionSupport>();

	public TodoService Todos { get; } = todos;

	public TimeProvider Clock { get; } = clock;

	/// <summary>Null when the store is ready, otherwise the failure to return.</summary>
	public async Task<ActionResult?> EnsureReadyAsync(CancellationToken cancellationToken) =>
		await runtime.Ready.WaitAsync(cancellationToken).ConfigureAwait(false)
			? null
			: ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.StoreUnavailable());

	/// <summary>Runs a store operation and maps known failures to results.</summary>
	public async Task<ActionResult> RunAsync(Func<Task<ActionResult>> operation, string actionId)
	{
		try
		{
			return await operation().ConfigureAwait(false);
		}
		catch (TodoValidationException exception)
		{
			return ActionResult.Failed(ActionErrorCodes.InvalidParameter, ValidationMessage(exception.Error));
		}
		catch (TodoStoreException exception)
		{
			_logger.Error(exception, "Action {ActionId} could not write the todo database", actionId);
			return ActionResult.Failed(ActionErrorCodes.ProviderError, Strings.Errors.StoreFailed());
		}
	}

	public static LocalizedText ValidationMessage(TodoValidationError error) => error switch
	{
		TodoValidationError.TitleRequired => Strings.Errors.TitleRequired(),
		TodoValidationError.TitleTooLong => Strings.Errors.TitleTooLong(TodoValidation.MaxTitleLength.ToString(CultureInfo.InvariantCulture)),
		_ => Strings.Errors.DescriptionTooLong(TodoValidation.MaxDescriptionLength.ToString(CultureInfo.InvariantCulture)),
	};

	public static string? ReadText(ActionExecutionContext context, string name) =>
		context.Parameters.GetValueOrDefault(name) switch
		{
			null => null,
			string text => text,
			var other => Convert.ToString(other, CultureInfo.InvariantCulture),
		};

	public static bool TryReadId(ActionExecutionContext context, out Guid id)
	{
		id = Guid.Empty;
		return ReadText(context, TodoIdParameter) is { } text && Guid.TryParse(text.Trim(), out id);
	}

	/// <summary>
	/// Reads a due date. The editor sends ISO 8601 UTC; a templated value may use any form
	/// <see cref="DueInputParser"/> accepts, where times without an offset are local to this computer.
	/// </summary>
	public bool TryReadDue(ActionExecutionContext context, string name, out DateTimeOffset? due)
	{
		var text = ReadText(context, name);
		if (string.IsNullOrWhiteSpace(text))
		{
			due = null;
			return true;
		}

		return DueInputParser.TryParse(text, CultureInfo.CurrentCulture, Clock.GetUtcNow(), TimeZoneInfo.Local, out due);
	}

	public static IReadOnlyList<ActionParameterOption> Options(IEnumerable<TodoItem> candidates, string? filter) =>
	[
		.. candidates
			.Where(t => string.IsNullOrWhiteSpace(filter) || t.Title.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase))
			.Select(t => new ActionParameterOption { Value = t.Id.ToString("D"), Label = t.Title }),
	];
}
