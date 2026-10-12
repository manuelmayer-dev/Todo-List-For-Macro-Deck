using NUnit.Framework;
using TodoList.Core.Settings;
using TodoList.Core.Storage;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class DataLocationTests
{
	private static string NewDirectory() => Path.Combine(Path.GetTempPath(), "todolist-tests", Guid.NewGuid().ToString("N"));

	private static DataLocationService Service(TestStore store, StorageLocation? location = null) =>
		new(location ?? new StorageLocation(store.Directory, TestStore.Logger), store.Database, store.Todos, store.Settings, TestStore.Logger);

	[Test]
	public async Task Moving_copies_todos_and_settings_and_is_remembered()
	{
		await using var store = await TestStore.CreateAsync();
		await store.Todos.CreateAsync(new TodoDraft("Milk"), CancellationToken.None);
		await store.Settings.SaveAsync(store.Settings.Current with { ShowCompleted = true }, CancellationToken.None);
		var target = NewDirectory();
		var changes = new List<TodoChangeKind>();
		store.Todos.Changed += (_, change) => changes.Add(change.Kind);

		var result = await Service(store).ChangeAsync(target, useExisting: false, CancellationToken.None);
		await store.Todos.CreateAsync(new TodoDraft("Bread"), CancellationToken.None);

		Assert.Multiple(() =>
		{
			Assert.That(result, Is.EqualTo(DataLocationResult.Moved));
			Assert.That(store.Database.FilePath, Is.EqualTo(Path.Combine(target, "todos.db")));
			Assert.That(changes[0], Is.EqualTo(TodoChangeKind.Reloaded));
			Assert.That(store.Settings.Current.ShowCompleted, Is.True);
			Assert.That(new StorageLocation(store.Directory, TestStore.Logger).StartupDirectory, Is.EqualTo(target));
		});

		// The new todo went to the new file, the old file still holds the copy from before.
		await using var moved = await TestStore.CreateAsync(target);
		await using var old = await TestStore.CreateAsync(store.Directory);
		Assert.That(moved.Todos.Snapshot.Items.Select(t => t.Title), Is.EquivalentTo(new[] { "Milk", "Bread" }));
		Assert.That(old.Todos.Snapshot.Items.Select(t => t.Title), Is.EquivalentTo(new[] { "Milk" }));
	}

	[Test]
	public async Task Existing_data_in_the_target_can_be_adopted()
	{
		var target = NewDirectory();
		var other = await TestStore.CreateAsync(target);
		await other.Todos.CreateAsync(new TodoDraft("From the other computer"), CancellationToken.None);
		await other.Settings.SaveAsync(other.Settings.Current with { ConfirmDelete = false }, CancellationToken.None);
		other.Close();

		await using var store = await TestStore.CreateAsync();
		await store.Todos.CreateAsync(new TodoDraft("Local"), CancellationToken.None);
		TodoSettings? announced = null;
		store.Settings.Changed += (_, settings) => announced = settings;
		var service = Service(store);

		Assert.That(service.HasOtherData(target), Is.True);
		var result = await service.ChangeAsync(target, useExisting: true, CancellationToken.None);

		Assert.Multiple(() =>
		{
			Assert.That(result, Is.EqualTo(DataLocationResult.UsedExisting));
			Assert.That(store.Todos.Snapshot.Items.Select(t => t.Title), Is.EquivalentTo(new[] { "From the other computer" }));
			Assert.That(announced?.ConfirmDelete, Is.False);
		});
	}

	[Test]
	public async Task Replacing_existing_data_keeps_a_backup()
	{
		var target = NewDirectory();
		var other = await TestStore.CreateAsync(target);
		await other.Todos.CreateAsync(new TodoDraft("Overwritten"), CancellationToken.None);
		other.Close();

		await using var store = await TestStore.CreateAsync();
		await store.Todos.CreateAsync(new TodoDraft("Local"), CancellationToken.None);

		await Service(store).ChangeAsync(target, useExisting: false, CancellationToken.None);

		Assert.Multiple(() =>
		{
			Assert.That(store.Todos.Snapshot.Items.Select(t => t.Title), Is.EquivalentTo(new[] { "Local" }));
			Assert.That(Directory.GetFiles(target, "todos.db.*.bak"), Has.Length.EqualTo(1));
		});
	}

	[Test]
	public async Task Going_back_to_the_default_folder_clears_the_choice()
	{
		await using var store = await TestStore.CreateAsync();
		var location = new StorageLocation(store.Directory, TestStore.Logger);
		var service = Service(store, location);
		await service.ChangeAsync(NewDirectory(), useExisting: false, CancellationToken.None);

		await service.ChangeAsync(store.Directory, useExisting: false, CancellationToken.None);

		Assert.Multiple(() =>
		{
			Assert.That(service.IsDefault, Is.True);
			Assert.That(location.CustomDirectory, Is.Null);
			Assert.That(new StorageLocation(store.Directory, TestStore.Logger).CustomDirectory, Is.Null);
		});
	}

	[Test]
	public void A_missing_custom_folder_falls_back_to_the_default()
	{
		var home = NewDirectory();
		var location = new StorageLocation(home, TestStore.Logger);
		var gone = NewDirectory();
		Directory.CreateDirectory(gone);
		location.Save(gone);
		Directory.Delete(gone);

		Assert.That(new StorageLocation(home, TestStore.Logger).StartupDirectory, Is.EqualTo(Path.GetFullPath(home)));
	}
}
