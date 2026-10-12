using System.Globalization;
using Microsoft.Data.Sqlite;
using Serilog;

namespace TodoList.Core.Storage;

/// <summary>
/// The SQLite file that holds todos and settings. Every method opens a pooled connection, so it is safe to
/// call concurrently; <see cref="TodoService"/> still serializes writes so snapshots stay consistent.
/// </summary>
public sealed class TodoDatabase : IDisposable
{
	private const string Columns =
		"id, title, description, created_at, due_at, completed_at, sort_order, due_notified_at, overdue_notified_at";

	private volatile string _connectionString;
	private readonly IReadOnlyList<Migration> _migrations;
	private readonly ILogger _logger;
	private volatile string _filePath;

	public TodoDatabase(string filePath, ILogger logger)
		: this(filePath, logger, Migrations.All)
	{
	}

	internal TodoDatabase(string filePath, ILogger logger, IReadOnlyList<Migration> migrations)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
		_filePath = Path.GetFullPath(filePath);
		_migrations = migrations;
		_logger = logger.ForContext<TodoDatabase>();
		_connectionString = ConnectionString(_filePath);
	}

	public string FilePath => _filePath;

	public int LatestSchemaVersion => _migrations.Count == 0 ? 0 : _migrations[^1].Version;

	/// <summary>
	/// Creates the file if needed and applies pending migrations. An existing database is backed up first.
	/// </summary>
	public async Task InitializeAsync(CancellationToken cancellationToken)
	{
		await RunAsync(async connection =>
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			await ExecuteAsync(connection, "PRAGMA journal_mode = WAL;", cancellationToken).ConfigureAwait(false);

			var current = await GetSchemaVersionAsync(connection, cancellationToken).ConfigureAwait(false);
			if (current > LatestSchemaVersion)
			{
				_logger.Warning(
					"Database schema version {Current} is newer than this plugin knows ({Latest}); continuing because migrations are additive",
					current,
					LatestSchemaVersion);
				return;
			}

			var pending = _migrations.Where(m => m.Version > current).ToList();
			if (pending.Count == 0)
			{
				return;
			}

			if (current > 0)
			{
				BackUp(connection, current);
			}

			foreach (var migration in pending)
			{
				await using var transaction = (SqliteTransaction)await connection
					.BeginTransactionAsync(cancellationToken)
					.ConfigureAwait(false);
				await ExecuteAsync(connection, migration.Sql, cancellationToken, transaction).ConfigureAwait(false);
				await ExecuteAsync(
					connection,
					string.Create(CultureInfo.InvariantCulture, $"PRAGMA user_version = {migration.Version};"),
					cancellationToken,
					transaction).ConfigureAwait(false);
				await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
				_logger.Information("Applied database migration {Version}", migration.Version);
			}
		}, "initialize the database", ensureDirectory: true).ConfigureAwait(false);
	}

	public Task<int> GetSchemaVersionAsync(CancellationToken cancellationToken) =>
		RunAsync(connection => GetSchemaVersionAsync(connection, cancellationToken), "read the schema version");

	public Task<IReadOnlyList<TodoItem>> LoadTodosAsync(CancellationToken cancellationToken) =>
		RunAsync<IReadOnlyList<TodoItem>>(async connection =>
		{
			await using var command = connection.CreateCommand();
			command.CommandText = $"SELECT {Columns} FROM todos;";
			await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			var items = new List<TodoItem>();
			while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				items.Add(ReadTodo(reader));
			}

			return items;
		}, "load todos");

	/// <summary>Inserts or replaces the given todos and deletes the given ids in one transaction.</summary>
	public Task SaveAsync(
		IReadOnlyCollection<TodoItem> upserts,
		IReadOnlyCollection<Guid> deletions,
		CancellationToken cancellationToken) =>
		RunAsync(async connection =>
		{
			await using var transaction = (SqliteTransaction)await connection
				.BeginTransactionAsync(cancellationToken)
				.ConfigureAwait(false);

			foreach (var todo in upserts)
			{
				await using var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText =
					$"INSERT OR REPLACE INTO todos ({Columns}) VALUES ($id, $title, $description, $created, $due, $completed, $order, $dueNotified, $overdueNotified);";
				command.Parameters.AddWithValue("$id", todo.Id.ToString("D"));
				command.Parameters.AddWithValue("$title", todo.Title);
				command.Parameters.AddWithValue("$description", (object?)todo.Description ?? DBNull.Value);
				command.Parameters.AddWithValue("$created", ToUnix(todo.CreatedAt));
				command.Parameters.AddWithValue("$due", ToUnixOrNull(todo.DueAt));
				command.Parameters.AddWithValue("$completed", ToUnixOrNull(todo.CompletedAt));
				command.Parameters.AddWithValue("$order", todo.SortOrder);
				command.Parameters.AddWithValue("$dueNotified", ToUnixOrNull(todo.DueNotifiedAt));
				command.Parameters.AddWithValue("$overdueNotified", ToUnixOrNull(todo.OverdueNotifiedAt));
				await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}

			foreach (var id in deletions)
			{
				await using var command = connection.CreateCommand();
				command.Transaction = transaction;
				command.CommandText = "DELETE FROM todos WHERE id = $id;";
				command.Parameters.AddWithValue("$id", id.ToString("D"));
				await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}

			await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
		}, "save todos");

	public Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken) =>
		RunAsync(async connection =>
		{
			await using var command = connection.CreateCommand();
			command.CommandText = "SELECT value FROM settings WHERE key = $key;";
			command.Parameters.AddWithValue("$key", key);
			return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;
		}, "read settings");

	public Task SetSettingAsync(string key, string value, CancellationToken cancellationToken) =>
		RunAsync(async connection =>
		{
			await using var command = connection.CreateCommand();
			command.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES ($key, $value);";
			command.Parameters.AddWithValue("$key", key);
			command.Parameters.AddWithValue("$value", value);
			await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
		}, "write settings");

	/// <summary>
	/// Points the database at <paramref name="filePath"/>. Unless <paramref name="adoptExisting"/>, the current
	/// contents are copied there first, and a file already there is backed up next to it. The caller must keep
	/// writes out while this runs.
	/// </summary>
	internal async Task SwitchToAsync(string filePath, bool adoptExisting, CancellationToken cancellationToken)
	{
		var target = Path.GetFullPath(filePath);
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(target)!);
			if (!adoptExisting)
			{
				if (File.Exists(target))
				{
					var backupPath = string.Create(CultureInfo.InvariantCulture, $"{target}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak");
					Copy(target, backupPath);
					_logger.Information("Backed up the existing database at {Path} to {BackupPath}", target, backupPath);
				}

				Copy(_filePath, target);
			}
		}
		catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
		{
			throw new TodoStoreException($"Could not move the todos to '{target}'.", exception);
		}

		var previous = _connectionString;
		_filePath = target;
		_connectionString = ConnectionString(target);
		ClearPool(previous);
		await InitializeAsync(cancellationToken).ConfigureAwait(false);
	}

	public void Dispose()
	{
		// Pooled connections keep the file open; release them so the plugin can shut down cleanly.
		ClearPool(_connectionString);
	}

	private static string ConnectionString(string filePath) => new SqliteConnectionStringBuilder
	{
		DataSource = filePath,
		Mode = SqliteOpenMode.ReadWriteCreate,
		Cache = SqliteCacheMode.Private,
		Pooling = true,
		DefaultTimeout = 10,
	}.ToString();

	private static void ClearPool(string connectionString)
	{
		using var connection = new SqliteConnection(connectionString);
		SqliteConnection.ClearPool(connection);
	}

	// The backup API copies a consistent image including pending WAL pages and replaces the target contents.
	private static void Copy(string sourcePath, string targetPath)
	{
		using var source = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = sourcePath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
		using var target = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = targetPath, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString());
		source.Open();
		source.BackupDatabase(target);
	}

	private async Task RunAsync(Func<SqliteConnection, Task> work, string operation, bool ensureDirectory = false) =>
		await RunAsync<object?>(async connection =>
		{
			await work(connection).ConfigureAwait(false);
			return null;
		}, operation, ensureDirectory).ConfigureAwait(false);

	private async Task<T> RunAsync<T>(Func<SqliteConnection, Task<T>> work, string operation, bool ensureDirectory = false)
	{
		try
		{
			if (ensureDirectory)
			{
				Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
			}

			await using var connection = new SqliteConnection(_connectionString);
			await connection.OpenAsync().ConfigureAwait(false);
			await ExecuteAsync(connection, "PRAGMA busy_timeout = 5000;", CancellationToken.None).ConfigureAwait(false);
			return await work(connection).ConfigureAwait(false);
		}
		catch (Exception exception) when (exception is SqliteException or IOException or UnauthorizedAccessException)
		{
			throw new TodoStoreException($"Could not {operation} in '{FilePath}'.", exception);
		}
	}

	private void BackUp(SqliteConnection connection, int version)
	{
		var backupPath = string.Create(CultureInfo.InvariantCulture, $"{FilePath}.v{version}.bak");
		using var backup = new SqliteConnection(new SqliteConnectionStringBuilder
		{
			DataSource = backupPath,
			Mode = SqliteOpenMode.ReadWriteCreate,
			Pooling = false,
		}.ToString());
		connection.BackupDatabase(backup);
		_logger.Information("Backed up the database to {BackupPath} before migrating", backupPath);
	}

	private static async Task<int> GetSchemaVersionAsync(SqliteConnection connection, CancellationToken cancellationToken)
	{
		await using var command = connection.CreateCommand();
		command.CommandText = "PRAGMA user_version;";
		var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
		return Convert.ToInt32(result, CultureInfo.InvariantCulture);
	}

	private static async Task ExecuteAsync(
		SqliteConnection connection,
		string sql,
		CancellationToken cancellationToken,
		SqliteTransaction? transaction = null)
	{
		await using var command = connection.CreateCommand();
		command.Transaction = transaction;
		command.CommandText = sql;
		await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
	}

	private static TodoItem ReadTodo(SqliteDataReader reader) => new()
	{
		Id = Guid.Parse(reader.GetString(0)),
		Title = reader.GetString(1),
		Description = reader.IsDBNull(2) ? null : reader.GetString(2),
		CreatedAt = FromUnix(reader.GetInt64(3)),
		DueAt = ReadTimestamp(reader, 4),
		CompletedAt = ReadTimestamp(reader, 5),
		SortOrder = reader.GetInt64(6),
		DueNotifiedAt = ReadTimestamp(reader, 7),
		OverdueNotifiedAt = ReadTimestamp(reader, 8),
	};

	private static DateTimeOffset? ReadTimestamp(SqliteDataReader reader, int ordinal) =>
		reader.IsDBNull(ordinal) ? null : FromUnix(reader.GetInt64(ordinal));

	private static long ToUnix(DateTimeOffset value) => value.ToUnixTimeMilliseconds();

	private static object ToUnixOrNull(DateTimeOffset? value) => value is { } v ? ToUnix(v) : DBNull.Value;

	private static DateTimeOffset FromUnix(long milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
}
