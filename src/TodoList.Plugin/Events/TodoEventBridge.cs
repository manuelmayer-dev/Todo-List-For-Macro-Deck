using System.Globalization;
using MacroDeck.Sdk.Events;
using TodoList.Core;
using TodoList.Core.Scheduling;
using TodoList.Core.Settings;

namespace TodoList.Plugin.Events;

/// <summary>
/// Turns committed todo changes and due transitions into Macro Deck events while a session is attached.
/// Completing and reopening raise only their own event, not also todo-updated.
/// </summary>
public sealed class TodoEventBridge : IDisposable
{
	private readonly TodoService _todos;
	private readonly DueMonitor _monitor;
	private readonly SettingsService _settings;
	private readonly TimeProvider _clock;
	private readonly Lock _lock = new();
	private IEventPublisher? _publisher;

	public TodoEventBridge(TodoService todos, DueMonitor monitor, SettingsService settings, TimeProvider clock)
	{
		_todos = todos;
		_monitor = monitor;
		_settings = settings;
		_clock = clock;
	}

	/// <summary>Starts publishing to <paramref name="publisher"/>. Safe to call again after a reconnect.</summary>
	public void Attach(IEventPublisher publisher)
	{
		lock (_lock)
		{
			if (_publisher is null)
			{
				_todos.Changed += OnTodosChanged;
				_monitor.TransitionOccurred += OnTransition;
			}

			_publisher = publisher;
		}
	}

	public void Detach()
	{
		lock (_lock)
		{
			if (_publisher is null)
			{
				return;
			}

			_todos.Changed -= OnTodosChanged;
			_monitor.TransitionOccurred -= OnTransition;
			_publisher = null;
		}
	}

	public void Dispose() => Detach();

	internal static Dictionary<string, object?> Payload(TodoItem todo, TodoStatus status) => new()
	{
		[TodoEvents.TodoIdKey] = todo.Id.ToString("D"),
		[TodoEvents.TitleKey] = todo.Title,
		[TodoEvents.DescriptionKey] = todo.Description ?? string.Empty,
		[TodoEvents.DueAtKey] = todo.DueAt?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
		[TodoEvents.StatusKey] = status.ToInvariantName(),
	};

	private void OnTodosChanged(object? sender, TodoChangedEventArgs change)
	{
		var eventId = change.Kind switch
		{
			TodoChangeKind.Created => TodoEvents.Created,
			TodoChangeKind.Updated => TodoEvents.Updated,
			TodoChangeKind.Completed => TodoEvents.Completed,
			TodoChangeKind.Reopened => TodoEvents.Reopened,
			TodoChangeKind.Deleted => TodoEvents.Deleted,
			_ => null,
		};

		if (eventId is null || change.Todo is not { } todo)
		{
			return;
		}

		var status = TodoStatusRules.Evaluate(todo, _clock.GetUtcNow(), _settings.Current.Thresholds);
		Publish(eventId, Payload(todo, status));
	}

	private void OnTransition(object? sender, DueTransition transition)
	{
		var (eventId, status) = transition.Kind == DueTransitionKind.Due
			? (TodoEvents.Due, TodoStatus.Due)
			: (TodoEvents.Overdue, TodoStatus.Overdue);
		Publish(eventId, Payload(transition.Todo, status));
	}

	private void Publish(string eventId, Dictionary<string, object?> payload)
	{
		IEventPublisher? publisher;
		lock (_lock)
		{
			publisher = _publisher;
		}

		publisher?.Publish(eventId, payload);
	}
}
