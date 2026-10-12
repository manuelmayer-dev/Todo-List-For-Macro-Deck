using System.Text.Json;
using System.Text.Json.Serialization;
using Serilog;
using TodoList.Core.Storage;

namespace TodoList.Core.Settings;

/// <summary>
/// Loads and saves <see cref="TodoSettings"/> as one JSON document, so new settings with defaults can be
/// added without a schema migration.
/// </summary>
public sealed class SettingsService : IDisposable
{
	private const string Key = "settings";

	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		Converters = { new JsonStringEnumConverter() },
	};

	private readonly TodoDatabase _database;
	private readonly ILogger _logger;
	private readonly SemaphoreSlim _writeGate = new(1, 1);
	private volatile TodoSettings _current = new();

	public SettingsService(TodoDatabase database, ILogger logger)
	{
		_database = database;
		_logger = logger.ForContext<SettingsService>();
	}

	public event EventHandler<TodoSettings>? Changed;

	public TodoSettings Current => _current;

	public void Dispose() => _writeGate.Dispose();

	public async Task LoadAsync(CancellationToken cancellationToken)
	{
		var json = await _database.GetSettingAsync(Key, cancellationToken).ConfigureAwait(false);
		if (json is null)
		{
			return;
		}

		try
		{
			_current = (JsonSerializer.Deserialize<TodoSettings>(json, JsonOptions) ?? new TodoSettings()).Normalize();
		}
		catch (JsonException exception)
		{
			_logger.Warning(exception, "Stored settings are unreadable; using defaults");
		}
	}

	/// <summary>Loads again after the database moved and tells subscribers when the values differ.</summary>
	public async Task ReloadAsync(CancellationToken cancellationToken)
	{
		var previous = _current;
		_current = new TodoSettings();
		await LoadAsync(cancellationToken).ConfigureAwait(false);
		if (_current != previous)
		{
			RaiseChanged(_current);
		}
	}

	public async Task SaveAsync(TodoSettings settings, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(settings);
		var normalized = settings.Normalize();

		await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			if (normalized == _current)
			{
				return;
			}

			await _database.SetSettingAsync(Key, JsonSerializer.Serialize(normalized, JsonOptions), cancellationToken)
				.ConfigureAwait(false);
			_current = normalized;
		}
		finally
		{
			_writeGate.Release();
		}

		RaiseChanged(normalized);
	}

	private void RaiseChanged(TodoSettings settings)
	{
		try
		{
			Changed?.Invoke(this, settings);
		}
		catch (Exception exception)
		{
			_logger.Error(exception, "A settings subscriber failed");
		}
	}
}
