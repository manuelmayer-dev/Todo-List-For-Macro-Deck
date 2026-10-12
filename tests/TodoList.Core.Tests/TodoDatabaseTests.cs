using Microsoft.Data.Sqlite;
using NUnit.Framework;
using TodoList.Core.Storage;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class TodoDatabaseTests
{
	[Test]
	public async Task A_new_database_is_at_the_latest_schema_version()
	{
		await using var store = await TestStore.CreateAsync();

		Assert.That(await store.Database.GetSchemaVersionAsync(CancellationToken.None), Is.EqualTo(store.Database.LatestSchemaVersion));
	}

	[Test]
	public async Task Upgrading_keeps_existing_todos_and_writes_a_backup()
	{
		await using var store = await TestStore.CreateAsync();
		var todo = await store.Todos.CreateAsync(new TodoDraft("Survives upgrades"), CancellationToken.None);
		store.Database.Dispose();

		var withNextVersion = Migrations.All.Append(new Migration(2, "ALTER TABLE todos ADD COLUMN color TEXT NULL;")).ToList();
		using var upgraded = new TodoDatabase(store.DatabasePath, TestStore.Logger, withNextVersion);
		await upgraded.InitializeAsync(CancellationToken.None);

		var items = await upgraded.LoadTodosAsync(CancellationToken.None);
		Assert.Multiple(async () =>
		{
			Assert.That(await upgraded.GetSchemaVersionAsync(CancellationToken.None), Is.EqualTo(2));
			Assert.That(items.Single().Id, Is.EqualTo(todo.Id));
			Assert.That(File.Exists(store.DatabasePath + ".v1.bak"), Is.True);
		});
	}

	[Test]
	public async Task A_database_from_a_newer_plugin_version_still_opens()
	{
		await using var store = await TestStore.CreateAsync();
		await store.Todos.CreateAsync(new TodoDraft("Rolled back"), CancellationToken.None);
		await using (var connection = new SqliteConnection($"Data Source={store.DatabasePath};Pooling=False"))
		{
			await connection.OpenAsync();
			await using var command = connection.CreateCommand();
			command.CommandText = "ALTER TABLE todos ADD COLUMN future TEXT NULL; PRAGMA user_version = 9;";
			await command.ExecuteNonQueryAsync();
		}

		await store.ReopenAsync();

		Assert.That(store.Todos.Snapshot.Items.Single().Title, Is.EqualTo("Rolled back"));
	}

	[Test]
	public void An_unwritable_location_fails_with_a_store_exception()
	{
		var blocker = Path.Combine(Path.GetTempPath(), "todolist-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
		File.WriteAllText(blocker, "a file where the data directory should be");
		try
		{
			using var database = new TodoDatabase(Path.Combine(blocker, "todos.db"), TestStore.Logger);

			Assert.ThrowsAsync<TodoStoreException>(() => database.InitializeAsync(CancellationToken.None));
		}
		finally
		{
			File.Delete(blocker);
		}
	}
}
