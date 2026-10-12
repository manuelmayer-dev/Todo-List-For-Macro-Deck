using NUnit.Framework;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class TodoServiceTests
{
	[Test]
	public async Task A_created_todo_is_persisted_across_a_restart()
	{
		await using var store = await TestStore.CreateAsync();
		var due = TestStore.Start.AddDays(1);

		var created = await store.Todos.CreateAsync(new TodoDraft("  Buy milk ", "  2 liters ", due), CancellationToken.None);
		await store.ReopenAsync();

		var reloaded = store.Todos.Snapshot.Find(created.Id);
		Assert.That(reloaded, Is.EqualTo(created));
		Assert.Multiple(() =>
		{
			Assert.That(reloaded!.Title, Is.EqualTo("Buy milk"));
			Assert.That(reloaded.Description, Is.EqualTo("2 liters"));
			Assert.That(reloaded.DueAt, Is.EqualTo(due));
			Assert.That(reloaded.CreatedAt, Is.EqualTo(TestStore.Start));
			Assert.That(reloaded.CreatedAt.Offset, Is.EqualTo(TimeSpan.Zero));
		});
	}

	[Test]
	public async Task A_blank_title_is_rejected_and_nothing_is_stored()
	{
		await using var store = await TestStore.CreateAsync();

		var error = Assert.ThrowsAsync<TodoValidationException>(() => store.Todos.CreateAsync(new TodoDraft("   "), CancellationToken.None));

		Assert.That(error!.Error, Is.EqualTo(TodoValidationError.TitleRequired));
		Assert.That(store.Todos.Snapshot.Items, Is.Empty);
	}

	[Test]
	public async Task Completing_and_reopening_records_and_clears_the_completion_time()
	{
		await using var store = await TestStore.CreateAsync();
		var todo = await store.Todos.CreateAsync(new TodoDraft("Call Anna"), CancellationToken.None);
		store.Clock.Advance(TimeSpan.FromMinutes(10));

		var completed = await store.Todos.CompleteAsync(todo.Id, CancellationToken.None);
		var reopened = await store.Todos.ReopenAsync(todo.Id, CancellationToken.None);

		Assert.Multiple(() =>
		{
			Assert.That(completed!.CompletedAt, Is.EqualTo(TestStore.Start.AddMinutes(10)));
			Assert.That(reopened!.IsCompleted, Is.False);
		});
	}

	[Test]
	public async Task Completing_twice_keeps_the_first_completion_time_and_raises_one_change()
	{
		await using var store = await TestStore.CreateAsync();
		var todo = await store.Todos.CreateAsync(new TodoDraft("Water plants"), CancellationToken.None);
		var changes = new List<TodoChangeKind>();
		store.Todos.Changed += (_, change) => changes.Add(change.Kind);

		await store.Todos.CompleteAsync(todo.Id, CancellationToken.None);
		store.Clock.Advance(TimeSpan.FromHours(1));
		var again = await store.Todos.CompleteAsync(todo.Id, CancellationToken.None);

		Assert.That(again!.CompletedAt, Is.EqualTo(TestStore.Start));
		Assert.That(changes, Is.EqualTo(new[] { TodoChangeKind.Completed }));
	}

	[Test]
	public async Task Operations_on_an_unknown_id_return_null()
	{
		await using var store = await TestStore.CreateAsync();
		var unknown = Guid.NewGuid();

		Assert.Multiple(async () =>
		{
			Assert.That(await store.Todos.CompleteAsync(unknown, CancellationToken.None), Is.Null);
			Assert.That(await store.Todos.ReopenAsync(unknown, CancellationToken.None), Is.Null);
			Assert.That(await store.Todos.DeleteAsync(unknown, CancellationToken.None), Is.Null);
			Assert.That(await store.Todos.UpdateAsync(unknown, new TodoDraft("x"), CancellationToken.None), Is.Null);
		});
	}

	[Test]
	public async Task A_deleted_todo_is_gone_after_a_restart()
	{
		await using var store = await TestStore.CreateAsync();
		var todo = await store.Todos.CreateAsync(new TodoDraft("Temporary"), CancellationToken.None);

		var deleted = await store.Todos.DeleteAsync(todo.Id, CancellationToken.None);
		await store.ReopenAsync();

		Assert.That(deleted!.Title, Is.EqualTo("Temporary"));
		Assert.That(store.Todos.Snapshot.Items, Is.Empty);
	}

	[Test]
	public async Task Changing_the_due_date_resets_the_transition_marks()
	{
		await using var store = await TestStore.CreateAsync();
		var todo = await store.Todos.CreateAsync(new TodoDraft("Report", DueAt: TestStore.Start.AddMinutes(-1)), CancellationToken.None);
		await store.Todos.MarkTransitionsAsync([new(Scheduling.DueTransitionKind.Overdue, todo)], TestStore.Start, CancellationToken.None);

		var titleOnly = await store.Todos.UpdateAsync(todo.Id, new TodoDraft("Report v2", DueAt: todo.DueAt), CancellationToken.None);
		var moved = await store.Todos.UpdateAsync(todo.Id, new TodoDraft("Report v2", DueAt: TestStore.Start.AddDays(1)), CancellationToken.None);

		Assert.Multiple(() =>
		{
			Assert.That(titleOnly!.OverdueNotifiedAt, Is.Not.Null);
			Assert.That(moved!.OverdueNotifiedAt, Is.Null);
			Assert.That(moved.DueNotifiedAt, Is.Null);
		});
	}

	[Test]
	public async Task Moving_a_todo_changes_the_manual_order_persistently()
	{
		await using var store = await TestStore.CreateAsync();
		var a = await store.Todos.CreateAsync(new TodoDraft("A"), CancellationToken.None);
		await store.Todos.CreateAsync(new TodoDraft("B"), CancellationToken.None);
		var c = await store.Todos.CreateAsync(new TodoDraft("C"), CancellationToken.None);

		Assert.That(await store.Todos.MoveAsync(c.Id, 0, CancellationToken.None), Is.True);
		await store.ReopenAsync();

		var manual = TodoOrdering.Sort(store.Todos.Snapshot.Items, TodoSortOrder.Manual, false, TestStore.Start, TodoThresholds.Default);
		Assert.That(manual.Select(t => t.Title), Is.EqualTo(new[] { "C", "A", "B" }));
		Assert.That(await store.Todos.MoveAsync(a.Id, 99, CancellationToken.None), Is.True);
	}

	[Test]
	public async Task Concurrent_creates_keep_every_todo()
	{
		await using var store = await TestStore.CreateAsync();

		await Task.WhenAll(Enumerable.Range(0, 40).Select(i =>
			Task.Run(() => store.Todos.CreateAsync(new TodoDraft($"Todo {i}"), CancellationToken.None))));
		await store.ReopenAsync();

		Assert.That(store.Todos.Snapshot.Items, Has.Count.EqualTo(40));
		Assert.That(store.Todos.Snapshot.Items.Select(t => t.SortOrder).Distinct().Count(), Is.EqualTo(40));
	}
}
