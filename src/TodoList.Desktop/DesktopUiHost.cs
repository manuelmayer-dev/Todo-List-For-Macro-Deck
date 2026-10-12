using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Serilog;
using TodoList.Core;
using TodoList.Core.Scheduling;
using TodoList.Core.Settings;
using TodoList.Core.Storage;

namespace TodoList.Desktop;

/// <summary>
/// Runs the desktop UI on the process main thread for as long as the plugin runs. macOS requires AppKit on
/// the main thread, so <see cref="Run"/> blocks there while the Macro Deck plugin host runs on the thread
/// pool. If the UI cannot start, the plugin keeps working without it.
/// </summary>
public sealed class DesktopUiHost : IDesktopUi, IDisposable
{
	// Bounded by Macro Deck's default shutdown grace period of ten seconds.
	private static readonly TimeSpan ShutdownWatchdog = TimeSpan.FromSeconds(5);

	private readonly TodoService _todos;
	private readonly SettingsService _settings;
	private readonly DataLocationService _location;
	private readonly TimeProvider _clock;
	private readonly MinuteClock _minuteClock;
	private readonly Task _storeReady;
	private readonly string _lockPath;
	private readonly ILogger _logger;
	private readonly Lock _lock = new();
	private IClassicDesktopStyleApplicationLifetime? _lifetime;
	private DesktopController? _controller;
	private bool _shutdownRequested;
	private volatile bool _uiRunning;
	private volatile bool _available;

	public DesktopUiHost(TodoService todos, SettingsService settings, DataLocationService location, TimeProvider clock, MinuteClock minuteClock, Task storeReady, string lockPath, ILogger logger)
	{
		_minuteClock = minuteClock;
		_todos = todos;
		_settings = settings;
		_location = location;
		_clock = clock;
		_storeReady = storeReady;
		_lockPath = lockPath;
		_logger = logger.ForContext<DesktopUiHost>();
	}

	public bool IsAvailable => _available;

	/// <summary>
	/// Runs the UI until <paramref name="pluginTask"/> completes. Must be called on the process main thread
	/// before anything else awaited there.
	/// </summary>
	public void Run(Task pluginTask, string[] args)
	{
		ArgumentNullException.ThrowIfNull(pluginTask);
		using var instanceLock = TryLockInstance();
		if (instanceLock is null)
		{
			_logger.Warning("Another process already shows the desktop todo list; this one stays headless");
			return;
		}

		_ = pluginTask.ContinueWith(_ => RequestShutdown(), CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);

		_uiRunning = true;
		try
		{
			Avalonia.Logging.Logger.Sink = new AvaloniaSerilogSink(_logger);
			AppBuilder
				.Configure(() => new DesktopApp(OnStarted))
				.UsePlatformDetect()
				.With(new MacOSPlatformOptions { ShowInDock = false, DisableDefaultApplicationMenuItems = true })
				.StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
		}
		catch (Exception exception)
		{
			_logger.Error(exception, "The desktop todo list could not start; todos remain available in Macro Deck");
		}
		finally
		{
			_uiRunning = false;
			_available = false;
			_controller?.Dispose();
			_controller = null;
		}

		if (!pluginTask.IsCompleted)
		{
			_logger.Warning("The desktop todo list stopped before the plugin; continuing without it");
		}
	}

	/// <summary>
	/// Called by the container on a worker thread at shutdown. AppKit and Avalonia objects may only be torn
	/// down on the UI thread, so this only asks that thread to stop; <see cref="Run"/> cleans up there.
	/// </summary>
	public void Dispose() => RequestShutdown();

	public bool ShowTodoPopup() => Post(controller => controller.ShowPopup());

	public bool ShowQuickAdd() => Post(controller => controller.ShowQuickAdd());

	private bool Post(Action<DesktopController> action)
	{
		if (!_available)
		{
			return false;
		}

		Dispatcher.UIThread.Post(() =>
		{
			if (_controller is { } controller)
			{
				action(controller);
			}
		});
		return true;
	}

	private void OnStarted(DesktopApp application, IClassicDesktopStyleApplicationLifetime lifetime)
	{
		bool shutdownRequested;
		lock (_lock)
		{
			_lifetime = lifetime;
			shutdownRequested = _shutdownRequested;
		}

		if (shutdownRequested)
		{
			lifetime.Shutdown();
			return;
		}

		Dispatcher.UIThread.UnhandledException += (_, e) =>
		{
			// A failing UI handler must not take the plugin down with it.
			_logger.Error(e.Exception, "Unhandled error in the desktop todo list");
			e.Handled = true;
		};
		lifetime.Exit += (_, _) =>
		{
			_available = false;
			_controller?.Dispose();
			_controller = null;
		};

		_controller = new DesktopController(application, _todos, _settings, _location, _clock, _minuteClock, _storeReady, _logger);
		_controller.Start();
		_available = true;
		_logger.Information("Desktop todo list started");
	}

	private void RequestShutdown()
	{
		IClassicDesktopStyleApplicationLifetime? lifetime;
		lock (_lock)
		{
			_shutdownRequested = true;
			lifetime = _lifetime;
		}

		_available = false;
		if (lifetime is not null)
		{
			Dispatcher.UIThread.Post(() => lifetime.Shutdown());
		}

		// If the UI thread is stuck, also while it is still starting, the plugin must still exit within Macro
		// Deck's grace period.
		_ = Task.Delay(ShutdownWatchdog).ContinueWith(_ =>
		{
			if (_uiRunning)
			{
				_logger.Warning("The desktop todo list did not stop in time; exiting");
				ProcessTermination.Exit(0);
			}
		}, TaskScheduler.Default);
	}

	private FileStream? TryLockInstance()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(_lockPath)!);
			return new FileStream(_lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException exception)
		{
			_logger.Warning(exception, "Could not create the desktop lock file {Path}", _lockPath);
			return null;
		}
	}
}
