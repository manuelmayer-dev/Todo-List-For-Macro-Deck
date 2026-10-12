using Serilog;
using TodoList.Core.Settings;

namespace TodoList.Core.Storage;

public enum DataLocationResult
{
	Unchanged,

	/// <summary>The current todos and settings were copied to the new folder.</summary>
	Moved,

	/// <summary>The folder already held todos, and the user chose to continue with those.</summary>
	UsedExisting,
}

/// <summary>
/// Moves the todo database to another folder while the plugin runs, for example into a synced folder.
/// The old file is left in place as a copy; data already in the target is backed up before it is replaced.
/// </summary>
public sealed class DataLocationService
{
	private readonly StorageLocation _location;
	private readonly TodoService _todos;
	private readonly SettingsService _settings;
	private readonly TodoDatabase _database;
	private readonly ILogger _logger;

	public DataLocationService(StorageLocation location, TodoDatabase database, TodoService todos, SettingsService settings, ILogger logger)
	{
		_location = location;
		_database = database;
		_todos = todos;
		_settings = settings;
		_logger = logger.ForContext<DataLocationService>();
	}

	public string DefaultDirectory => _location.DefaultDirectory;

	public string CurrentDirectory => Path.GetDirectoryName(_database.FilePath)!;

	public bool IsDefault => StorageLocation.SamePath(CurrentDirectory, DefaultDirectory);

	/// <summary>True when <paramref name="directory"/> already holds a todo database other than the current one.</summary>
	public bool HasOtherData(string directory) =>
		!StorageLocation.SamePath(directory, CurrentDirectory) &&
		File.Exists(StorageLocation.DatabasePathIn(directory));

	/// <summary>
	/// Switches to <paramref name="directory"/>. With existing data there, <paramref name="useExisting"/> picks
	/// between continuing with it and replacing it with the current todos.
	/// </summary>
	public async Task<DataLocationResult> ChangeAsync(string directory, bool useExisting, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(directory);
		var target = Path.GetFullPath(directory);
		if (StorageLocation.SamePath(target, CurrentDirectory))
		{
			return DataLocationResult.Unchanged;
		}

		var adopt = useExisting && File.Exists(StorageLocation.DatabasePathIn(target));
		await _todos.RelocateAsync(StorageLocation.DatabasePathIn(target), adopt, cancellationToken).ConfigureAwait(false);
		try
		{
			_location.Save(target);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
		{
			throw new TodoStoreException($"Could not remember the data folder '{target}'.", exception);
		}

		await _settings.ReloadAsync(cancellationToken).ConfigureAwait(false);
		_logger.Information("Todo data now lives in {Directory}", target);
		return adopt ? DataLocationResult.UsedExisting : DataLocationResult.Moved;
	}
}
