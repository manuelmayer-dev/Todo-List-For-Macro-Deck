using MacroDeck.Plugin.Testing;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using TodoList.Core;

namespace TodoList.Plugin.Tests;

[TestFixture]
public sealed class EventAndVariableTests
{
	private static string Payload(PublishedEvent published, string key) =>
		published.Parameters!.Value.GetProperty(key).ToString();

	[Test]
	public async Task Lifecycle_changes_publish_one_event_each_with_the_todo_payload()
	{
		await using var plugin = await PluginFixture.StartAsync();
		var due = plugin.Now.AddDays(2);

		var todo = await plugin.AddAsync("Pay rent", due);
		await plugin.Todos.UpdateAsync(todo.Id, new TodoDraft("Pay rent today", DueAt: due), CancellationToken.None);
		await plugin.Todos.CompleteAsync(todo.Id, CancellationToken.None);
		await plugin.Todos.ReopenAsync(todo.Id, CancellationToken.None);
		await plugin.Todos.DeleteAsync(todo.Id, CancellationToken.None);

		var ids = plugin.Harness.Context.Events.Published.Select(e => e.EventId);
		Assert.That(ids, Is.EqualTo(new[] { "todo-created", "todo-updated", "todo-completed", "todo-reopened", "todo-deleted" }));

		var created = plugin.Events("todo-created").Single();
		Assert.Multiple(() =>
		{
			Assert.That(Payload(created, "todoId"), Is.EqualTo(todo.Id.ToString("D")));
			Assert.That(Payload(created, "title"), Is.EqualTo("Pay rent"));
			Assert.That(DateTimeOffset.Parse(Payload(created, "dueAt"), System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(due));
			Assert.That(Payload(created, "status"), Is.EqualTo("open"));
			Assert.That(Payload(plugin.Events("todo-completed").Single(), "status"), Is.EqualTo("completed"));
		});
	}

	[Test]
	public async Task Due_and_overdue_events_fire_as_time_passes_and_only_once()
	{
		await using var plugin = await PluginFixture.StartAsync();
		await plugin.AddAsync("Standup", plugin.Now.AddMinutes(90));

		plugin.Harness.Clock.Advance(TimeSpan.FromMinutes(30));
		await Wait.UntilAsync(() => plugin.Events("todo-due").Any(), TimeSpan.FromSeconds(5));
		await Wait.UntilAsync(() => Monitor(plugin).NextCheckAt > plugin.Now, TimeSpan.FromSeconds(5));
		plugin.Harness.Clock.Advance(TimeSpan.FromMinutes(61));
		await Wait.UntilAsync(() => plugin.Events("todo-overdue").Any(), TimeSpan.FromSeconds(5));
		await plugin.Harness.InitializeIntegrationsAsync();

		Assert.That(plugin.Events("todo-due").Count(), Is.EqualTo(1));
		Assert.That(plugin.Events("todo-overdue").Count(), Is.EqualTo(1));
		Assert.That(Payload(plugin.Events("todo-overdue").Single(), "status"), Is.EqualTo("overdue"));
	}

	[Test]
	public async Task Due_events_wait_while_macro_deck_is_disconnected_instead_of_being_lost()
	{
		await using var plugin = await PluginFixture.StartAsync();
		plugin.Delivery.IsOpen = false;
		await plugin.AddAsync("Offline deadline", plugin.Now.AddMinutes(-1));
		await Monitor(plugin).ReconcileAsync(CancellationToken.None);

		var heldWhileDisconnected = plugin.Events("todo-overdue").Count();
		plugin.Delivery.IsOpen = true;
		await Monitor(plugin).ReconcileAsync(CancellationToken.None);

		Assert.That(heldWhileDisconnected, Is.Zero);
		Assert.That(plugin.Events("todo-overdue").Count(), Is.EqualTo(1));
	}

	[Test]
	public async Task Initializing_again_does_not_duplicate_events()
	{
		await using var plugin = await PluginFixture.StartAsync();

		await plugin.Harness.InitializeIntegrationsAsync();
		await plugin.AddAsync("Once");

		Assert.That(plugin.Events("todo-created").Count(), Is.EqualTo(1));
	}

	[Test]
	public async Task Count_variables_follow_the_shared_status_rules()
	{
		await using var plugin = await PluginFixture.StartAsync();
		await plugin.AddAsync("Overdue", plugin.Now.AddMinutes(-5));
		await plugin.AddAsync("Due soon", plugin.Now.AddMinutes(30));
		await plugin.AddAsync("Someday");
		var done = await plugin.AddAsync("Done");
		await plugin.Todos.CompleteAsync(done.Id, CancellationToken.None);

		Assert.Multiple(async () =>
		{
			Assert.That(await Number(plugin, "open-count"), Is.EqualTo(3));
			Assert.That(await Number(plugin, "completed-count"), Is.EqualTo(1));
			Assert.That(await Number(plugin, "due-count"), Is.EqualTo(1));
			Assert.That(await Number(plugin, "overdue-count"), Is.EqualTo(1));
			Assert.That(await Number(plugin, "total-count"), Is.EqualTo(4));
		});
	}

	[Test]
	public async Task Next_variables_describe_the_earliest_deadline_and_go_negative_when_overdue()
	{
		await using var plugin = await PluginFixture.StartAsync();
		await plugin.AddAsync("Later", plugin.Now.AddHours(3));
		await plugin.AddAsync("Late", plugin.Now.AddMinutes(-10));

		Assert.That(await Text(plugin, "next-title"), Is.EqualTo("Late"));
		Assert.That(await Number(plugin, "next-due-in-minutes"), Is.EqualTo(-10));
		Assert.That(await Text(plugin, "next-due-at"), Is.Not.Empty);
	}

	[Test]
	public async Task Next_variables_are_unavailable_without_open_todos()
	{
		await using var plugin = await PluginFixture.StartAsync();

		var outcome = await plugin.Harness.Variables.GetAsync("next-title");

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(outcome.Data!.Value.GetProperty("value").GetProperty("kind").GetString(), Is.EqualTo("unavailable"));
	}

	private static Core.Scheduling.DueMonitor Monitor(PluginFixture plugin) =>
		plugin.Harness.Services.GetRequiredService<Core.Scheduling.DueMonitor>();

	private static async Task<double> Number(PluginFixture plugin, string id)
	{
		var outcome = await plugin.Harness.Variables.GetAsync(id);
		Assert.That(outcome.Succeeded, Is.True, id);
		return outcome.Data!.Value.GetProperty("value").GetProperty("number").GetDouble();
	}

	private static async Task<string> Text(PluginFixture plugin, string id)
	{
		var outcome = await plugin.Harness.Variables.GetAsync(id);
		Assert.That(outcome.Succeeded, Is.True, id);
		return outcome.Data!.Value.GetProperty("value").GetProperty("text").GetString() ?? string.Empty;
	}
}
