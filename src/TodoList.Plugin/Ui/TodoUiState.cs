using MacroDeck.Ui.Runtime;
using TodoList.Core;
using TodoList.Core.Settings;

namespace TodoList.Plugin.Ui;

public enum TodoStoreStatus
{
	Loading,
	Ready,
	Failed,
}

/// <summary>
/// The reactive state every widget and modal session reads. Writing it from any thread re-renders only the
/// parts of open views that read the changed value.
/// </summary>
public sealed class TodoUiState(TodoSnapshot snapshot, TodoSettings settings, DateTimeOffset now, TodoStoreStatus status)
{
	public UiState<TodoSnapshot> Snapshot { get; } = new(snapshot);

	public UiState<TodoSettings> Settings { get; } = new(settings);

	public UiState<DateTimeOffset> Now { get; } = new(now);

	public UiState<TodoStoreStatus> Status { get; } = new(status);

	public TodoThresholds Thresholds => Settings.Value.Thresholds;
}
