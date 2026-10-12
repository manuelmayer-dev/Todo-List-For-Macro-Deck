using MacroDeck.Plugin.Testing;
using Serilog;
using TodoList.Core.Settings;
using TodoList.Core.Storage;

namespace TodoList.Core.Tests;

/// <summary>
/// A real SQLite database in a temporary directory plus the services on top of it.
/// </summary>
internal sealed class TestStore : IAsyncDisposable
{
	public static readonly DateTimeOffset Start = new(2026, 3, 10, 12, 0, 0, TimeSpan.Zero);

	private TestStore(string directory, ManualTimeProvider clock)
	{
		Directory = directory;
		Clock = clock;
		Database = new TodoDatabase(DatabasePath, Logger);
		Todos = new TodoService(Database, Clock, Logger);
		Settings = new SettingsService(Database, Logger);
	}

	public static ILogger Logger { get; } = new LoggerConfiguration().CreateLogger();

	public string Directory { get; }

	public string DatabasePath => Path.Combine(Directory, "todos.db");

	public ManualTimeProvider Clock { get; }

	public TodoDatabase Database { get; private set; }

	public TodoService Todos { get; private set; }

	public SettingsService Settings { get; private set; }

	public static async Task<TestStore> CreateAsync(string? directory = null, ManualTimeProvider? clock = null)
	{
		var store = new TestStore(
			directory ?? Path.Combine(Path.GetTempPath(), "todolist-tests", Guid.NewGuid().ToString("N")),
			clock ?? new ManualTimeProvider(Start));
		await store.LoadAsync();
		return store;
	}

	/// <summary>Simulates a plugin restart: new services over the same file.</summary>
	public async Task<TestStore> ReopenAsync()
	{
		DisposeServices();
		Database = new TodoDatabase(DatabasePath, Logger);
		Todos = new TodoService(Database, Clock, Logger);
		Settings = new SettingsService(Database, Logger);
		await LoadAsync();
		return this;
	}

	/// <summary>Releases the file but leaves the directory, for a store another test step opens later.</summary>
	public void Close() => DisposeServices();

	public ValueTask DisposeAsync()
	{
		DisposeServices();
		try
		{
			System.IO.Directory.Delete(Directory, recursive: true);
		}
		catch (IOException)
		{
		}

		return ValueTask.CompletedTask;
	}

	private async Task LoadAsync()
	{
		await Todos.LoadAsync(CancellationToken.None);
		await Settings.LoadAsync(CancellationToken.None);
	}

	private void DisposeServices()
	{
		Todos.Dispose();
		Settings.Dispose();
		Database.Dispose();
	}
}
