using Microsoft.Extensions.Hosting;
using Serilog;
using TodoList.Core;
using TodoList.Core.Scheduling;
using TodoList.Core.Settings;
using TodoList.Core.Storage;

namespace TodoList.Plugin.Hosting;

/// <summary>
/// Opens the database when the process starts, before Macro Deck's session exists, so the desktop UI and the
/// integration share one loaded store. It never throws: a failure leaves <see cref="Ready"/> false and every
/// surface reports the store as unavailable instead of the process crashing into a restart loop.
/// </summary>
public sealed class TodoRuntime : IHostedService, IDisposable
{
	private readonly TodoDatabase _database;
	private readonly TodoService _todos;
	private readonly SettingsService _settings;
	private readonly DueMonitor _monitor;
	private readonly MinuteClock _minuteClock;
	private readonly TodoDataPaths _paths;
	private readonly ILogger _logger;
	private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
	private readonly CancellationTokenSource _stopping = new();

	public TodoRuntime(
		TodoDatabase database,
		TodoService todos,
		SettingsService settings,
		DueMonitor monitor,
		MinuteClock minuteClock,
		TodoDataPaths paths,
		ILogger logger)
	{
		_database = database;
		_todos = todos;
		_settings = settings;
		_monitor = monitor;
		_minuteClock = minuteClock;
		_paths = paths;
		_logger = logger.ForContext<TodoRuntime>();
	}

	/// <summary>True once todos and settings are loaded; false if the store could not be opened.</summary>
	public Task<bool> Ready => _ready.Task;

	public Task StartAsync(CancellationToken cancellationToken)
	{
		if (_paths.IsDevelopmentFallback)
		{
			_logger.Warning("Macro Deck provided no plugin data directory; using {Directory}", _paths.DataDirectory);
		}

		_ = Task.Run(LoadAsync, CancellationToken.None);
		return Task.CompletedTask;
	}

	public async Task StopAsync(CancellationToken cancellationToken)
	{
		await _stopping.CancelAsync().ConfigureAwait(false);
		_monitor.Stop();
		_minuteClock.Dispose();
		_database.Dispose();
	}

	public void Dispose() => _stopping.Dispose();

	private async Task LoadAsync()
	{
		try
		{
			// Loading todos first migrates the database and settles TodoService.Ready either way.
			await _todos.LoadAsync(_stopping.Token).ConfigureAwait(false);
			await _settings.LoadAsync(_stopping.Token).ConfigureAwait(false);
			_ready.TrySetResult(true);
		}
		catch (OperationCanceledException)
		{
			_ready.TrySetResult(false);
		}
		catch (Exception exception)
		{
			_logger.Error(exception, "The todo database at {Path} could not be opened; todos are unavailable", _database.FilePath);
			_ready.TrySetResult(false);
		}
	}
}
