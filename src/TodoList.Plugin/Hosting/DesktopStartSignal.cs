namespace TodoList.Plugin.Hosting;

/// <summary>
/// Completes once the integration has initialized for the first time. The desktop UI waits for it, because
/// starting Avalonia and Skia is heavy enough to delay the first Macro Deck session if both run at once.
/// </summary>
public sealed class DesktopStartSignal
{
	private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private int _pending = 2;

	public Task Ready => _ready.Task;

	/// <summary>Called by each of the integration's two initialization steps; the second one opens the signal.</summary>
	public void StepCompleted()
	{
		if (Interlocked.Decrement(ref _pending) <= 0)
		{
			_ready.TrySetResult();
		}
	}
}
