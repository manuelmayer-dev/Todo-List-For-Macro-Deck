namespace TodoList.Core.Scheduling;

/// <summary>
/// Says whether a due transition raised now would reach its listeners. While it is closed,
/// <see cref="DueMonitor"/> records nothing, so the transition is raised once delivery is possible again.
/// </summary>
public interface ITransitionDeliveryGate
{
	bool IsOpen { get; }
}
