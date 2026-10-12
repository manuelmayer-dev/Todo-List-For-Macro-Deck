using MacroDeck.Plugin.Testing;
using Microsoft.Extensions.DependencyInjection;
using TodoList.Core;
using TodoList.Core.Scheduling;
using TodoList.Plugin.Hosting;

namespace TodoList.Plugin.Tests;

/// <summary>
/// The plugin built the way Program builds it, against a temporary data directory. The harness does not
/// start hosted services, so the runtime that opens the database is started here.
/// </summary>
internal sealed class PluginFixture : IAsyncDisposable
{
	private PluginFixture(PluginTestHarness harness, string directory, TestDeliveryGate delivery)
	{
		Delivery = delivery;
		Harness = harness;
		Directory = directory;
	}

	public TestDeliveryGate Delivery { get; }

	public PluginTestHarness Harness { get; }

	public string Directory { get; }

	public TodoService Todos => Harness.Services.GetRequiredService<TodoService>();

	public DateTimeOffset Now => Harness.Clock.GetUtcNow();

	public static async Task<PluginFixture> StartAsync(string? dataDirectory = null)
	{
		var delivery = new TestDeliveryGate();
		var directory = dataDirectory ?? Path.Combine(Path.GetTempPath(), "todolist-plugin-tests", Guid.NewGuid().ToString("N"));
		var harness = PluginTestHarness.Create(builder =>
		{
			builder.UseLocalization(Strings.LocalizationCatalog).RegisterIntegration<TodoIntegration>();
			builder.Services.AddTodoList(options => options.DataDirectory = directory);

			// The harness never opens a real session, so delivery is controlled by the test.
			builder.Services.AddSingleton<ITransitionDeliveryGate>(delivery);
		});

		var runtime = harness.Services.GetRequiredService<TodoRuntime>();
		await runtime.StartAsync(CancellationToken.None);
		await runtime.Ready.WaitAsync(TimeSpan.FromSeconds(10));
		await harness.InitializeIntegrationsAsync();
		return new PluginFixture(harness, directory, delivery);
	}

	public Task<TodoItem> AddAsync(string title, DateTimeOffset? dueAt = null) =>
		Todos.CreateAsync(new TodoDraft(title, DueAt: dueAt), CancellationToken.None);

	public IEnumerable<PublishedEvent> Events(string eventId) =>
		Harness.Context.Events.Published.Where(e => e.EventId == eventId);

	public async ValueTask DisposeAsync()
	{
		await Harness.Services.GetRequiredService<TodoRuntime>().StopAsync(CancellationToken.None);
		await Harness.DisposeAsync();
		try
		{
			System.IO.Directory.Delete(Directory, recursive: true);
		}
		catch (IOException)
		{
		}
	}
}

internal sealed class TestDeliveryGate : ITransitionDeliveryGate
{
	public bool IsOpen { get; set; } = true;
}
