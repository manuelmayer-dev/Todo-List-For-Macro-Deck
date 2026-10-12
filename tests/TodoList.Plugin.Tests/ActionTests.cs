using MacroDeck.Sdk.Actions;
using NUnit.Framework;

namespace TodoList.Plugin.Tests;

[TestFixture]
public sealed class ActionTests
{
	private static Dictionary<string, object?> Params(params (string Key, object? Value)[] values) =>
		values.ToDictionary(v => v.Key, v => v.Value);

	[Test]
	public async Task Create_adds_a_todo_with_the_given_fields()
	{
		await using var plugin = await PluginFixture.StartAsync();

		var outcome = await plugin.Harness.Actions.ExecuteAsync("create-todo", Params(
			("title", " Call Anna "),
			("description", "About the offsite"),
			("dueAt", "2030-05-01T08:30:00.000Z")));

		Assert.That(outcome.Succeeded, Is.True);
		var todo = plugin.Todos.Snapshot.Items.Single();
		Assert.Multiple(() =>
		{
			Assert.That(todo.Title, Is.EqualTo("Call Anna"));
			Assert.That(todo.Description, Is.EqualTo("About the offsite"));
			Assert.That(todo.DueAt, Is.EqualTo(new DateTimeOffset(2030, 5, 1, 8, 30, 0, TimeSpan.Zero)));
		});
	}

	[Test]
	public async Task Create_accepts_a_relative_due_date_from_a_template()
	{
		await using var plugin = await PluginFixture.StartAsync();

		var outcome = await plugin.Harness.Actions.ExecuteAsync("create-todo", Params(("title", "Stretch"), ("dueAt", "+2h")));

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(plugin.Todos.Snapshot.Items.Single().DueAt, Is.EqualTo(plugin.Now.AddHours(2)));
	}

	[TestCase("   ", null)]
	[TestCase("Valid title", "next tuesday-ish")]
	public async Task Create_fails_on_invalid_input_and_stores_nothing(string title, string? dueAt)
	{
		await using var plugin = await PluginFixture.StartAsync();

		var outcome = await plugin.Harness.Actions.ExecuteAsync("create-todo", Params(("title", title), ("dueAt", dueAt)));

		Assert.That(outcome.Succeeded, Is.False);
		Assert.That(outcome.Error?.Code, Is.EqualTo(ActionErrorCodes.InvalidParameter));
		Assert.That(plugin.Todos.Snapshot.Items, Is.Empty);
	}

	[Test]
	public async Task Complete_without_a_choice_completes_the_todo_with_the_earliest_deadline()
	{
		await using var plugin = await PluginFixture.StartAsync();
		await plugin.AddAsync("No deadline");
		var soon = await plugin.AddAsync("Soon", plugin.Now.AddHours(1));
		await plugin.AddAsync("Later", plugin.Now.AddDays(1));

		var outcome = await plugin.Harness.Actions.ExecuteAsync("complete-todo", Params(("todoId", "")));

		Assert.That(outcome.Succeeded, Is.True);
		Assert.That(plugin.Todos.Snapshot.Find(soon.Id)!.IsCompleted, Is.True);
		Assert.That(plugin.Todos.Snapshot.Completed.Count(), Is.EqualTo(1));
	}

	[Test]
	public async Task Complete_without_open_todos_fails_with_not_found()
	{
		await using var plugin = await PluginFixture.StartAsync();

		var outcome = await plugin.Harness.Actions.ExecuteAsync("complete-todo", Params(("todoId", null)));

		Assert.That(outcome.Error?.Code, Is.EqualTo(ActionErrorCodes.NotFound));
	}

	[TestCase("complete-todo")]
	[TestCase("reopen-todo")]
	[TestCase("delete-todo")]
	public async Task Unknown_or_malformed_ids_fail_truthfully(string actionId)
	{
		await using var plugin = await PluginFixture.StartAsync();

		var unknown = await plugin.Harness.Actions.ExecuteAsync(actionId, Params(("todoId", Guid.NewGuid().ToString())));
		var garbage = await plugin.Harness.Actions.ExecuteAsync(actionId, Params(("todoId", "not-an-id")));

		Assert.That(unknown.Error?.Code, Is.EqualTo(ActionErrorCodes.NotFound));
		Assert.That(garbage.Error?.Code, Is.EqualTo(ActionErrorCodes.InvalidParameter));
	}

	[Test]
	public async Task Reopen_and_delete_change_the_chosen_todo()
	{
		await using var plugin = await PluginFixture.StartAsync();
		var todo = await plugin.AddAsync("Errand");
		await plugin.Todos.CompleteAsync(todo.Id, CancellationToken.None);

		var reopened = await plugin.Harness.Actions.ExecuteAsync("reopen-todo", Params(("todoId", todo.Id.ToString())));
		var deleted = await plugin.Harness.Actions.ExecuteAsync("delete-todo", Params(("todoId", todo.Id.ToString())));

		Assert.That(reopened.Succeeded && deleted.Succeeded, Is.True);
		Assert.That(plugin.Todos.Snapshot.Items, Is.Empty);
	}

	[Test]
	public async Task Reopening_an_open_todo_is_a_successful_no_op()
	{
		await using var plugin = await PluginFixture.StartAsync();
		var todo = await plugin.AddAsync("Already open");

		var outcome = await plugin.Harness.Actions.ExecuteAsync("reopen-todo", Params(("todoId", todo.Id.ToString())));

		Assert.That(outcome.Succeeded, Is.True);
	}

	[Test]
	public async Task Dynamic_options_list_matching_open_or_completed_todos()
	{
		await using var plugin = await PluginFixture.StartAsync();
		await plugin.AddAsync("Buy milk");
		await plugin.AddAsync("Buy bread");
		var done = await plugin.AddAsync("Buy eggs");
		await plugin.Todos.CompleteAsync(done.Id, CancellationToken.None);

		var open = await plugin.Harness.Actions.GetOptionsAsync("complete-todo", "todoId", filter: "milk");
		var completed = await plugin.Harness.Actions.GetOptionsAsync("reopen-todo", "todoId");

		Assert.That(Labels(open), Is.EqualTo(new[] { "Buy milk" }));
		Assert.That(Labels(completed), Is.EqualTo(new[] { "Buy eggs" }));
	}

	[Test]
	public async Task Opening_the_list_without_a_client_fails_as_unavailable()
	{
		await using var plugin = await PluginFixture.StartAsync();

		var outcome = await plugin.Harness.Actions.ExecuteAsync("open-todo-list");

		Assert.That(outcome.Error?.Code, Is.EqualTo(ActionErrorCodes.Unavailable));
	}

	[Test]
	public async Task Opening_the_desktop_list_fails_when_the_desktop_ui_is_not_running()
	{
		await using var plugin = await PluginFixture.StartAsync();

		var outcome = await plugin.Harness.Actions.ExecuteAsync("open-desktop-todo-list");

		Assert.That(outcome.Error?.Code, Is.EqualTo(ActionErrorCodes.Unavailable));
	}

	private static string[] Labels(MacroDeck.Plugin.Testing.CapabilityInvocationOutcome outcome)
	{
		Assert.That(outcome.Succeeded, Is.True);
		var options = outcome.Data!.Value.GetProperty("options");
		return [.. options.EnumerateArray().Select(o => o.GetProperty("label").ToString())];
	}
}
