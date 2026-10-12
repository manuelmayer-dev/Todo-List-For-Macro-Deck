using System.Diagnostics;

namespace TodoList.Desktop;

/// <summary>
/// Ends the process even when something inside it refuses to let go. <see cref="Environment.Exit"/> first runs
/// every ProcessExit handler, and libraries register blocking ones (Microsoft.Data.Sqlite clears its pools under
/// a lock, Avalonia's Windows compositor stops its render threads); one that hangs keeps the process alive
/// forever. A dedicated thread, not the thread pool, kills the process if the orderly exit does not finish.
/// </summary>
public static class ProcessTermination
{
	private static readonly TimeSpan ExitGrace = TimeSpan.FromSeconds(1);

	/// <summary>Exits with <paramref name="exitCode"/>, or kills the process if exiting takes too long.</summary>
	public static void Exit(int exitCode)
	{
		StartThread("Process exit watchdog", ExitGrace, Kill);
		Environment.Exit(exitCode);
	}

	/// <summary>Calls <see cref="Exit"/> after <paramref name="delay"/>, independent of a busy thread pool.</summary>
	public static void ExitAfter(TimeSpan delay, int exitCode) => StartThread("Process stop deadline", delay, () => Exit(exitCode));

	private static void StartThread(string name, TimeSpan delay, Action action)
	{
		var thread = new Thread(() =>
		{
			Thread.Sleep(delay);
			action();
		})
		{
			IsBackground = true,
			Name = name,
		};
		thread.Start();
	}

	private static void Kill()
	{
		using var process = Process.GetCurrentProcess();
		process.Kill();
	}
}
