using System.Text.Json;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Surfaces;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using TodoList.Core;
using TodoList.Plugin.Ui;

namespace TodoList.Plugin.Tests;

[TestFixture]
public sealed class UiTests
{
	private const string QualifiedWidgetType = "com.suchbyte.todo-list-plugin::todo-list";

	[Test]
	public async Task The_widget_type_opens_the_list_on_a_short_press()
	{
		await using var plugin = await PluginFixture.StartAsync();

		var outcome = await plugin.Harness.WidgetTypeProvider.GetWidgetTypesAsync();

		Assert.That(outcome.Succeeded, Is.True);
		var json = outcome.Data!.Value.ToString();
		Assert.That(json, Does.Contain("\"todo-list\""));
		Assert.That(json, Does.Contain("open-todo-list"));
	}

	[TestCase(UiSurfaceKinds.Widget)]
	[TestCase(UiSurfaceKinds.Preview)]
	public async Task Widget_surfaces_of_this_type_get_a_session(string kind)
	{
		await using var plugin = await PluginFixture.StartAsync();
		await plugin.AddAsync("Visible on the deck", plugin.Now.AddMinutes(20));

		await using var session = await OpenAsync(plugin, kind, new()
		{
			[UiWidgetSurfaceAttributes.WidgetType] = Json(QualifiedWidgetType),
			[UiWidgetSurfaceAttributes.Data] = JsonDocument.Parse("""{"maxVisible":3,"density":"compact"}""").RootElement,
		});

		Assert.That(session, Is.Not.Null);
		Assert.That(session!.BuildTree(), Is.Not.Null);
	}

	[Test]
	public async Task The_modal_and_the_widget_configuration_get_sessions()
	{
		await using var plugin = await PluginFixture.StartAsync();

		await using var modal = await OpenAsync(plugin, UiSurfaceKinds.Dialog, new() { [UiDialogSurfaceAttributes.ViewId] = Json(TodoModalView.ViewId) });
		await using var config = await OpenAsync(plugin, UiSurfaceKinds.Config, new()
		{
			[UiConfigSurfaceAttributes.EntryPoint] = Json(UiConfigEntryPoints.WidgetConfig),
			[UiConfigSurfaceAttributes.WidgetType] = Json(QualifiedWidgetType),
			[UiConfigSurfaceAttributes.WidgetData] = JsonDocument.Parse("{}").RootElement,
		});

		Assert.That(modal?.BuildTree(), Is.Not.Null);
		Assert.That(config?.BuildTree(), Is.Not.Null);
	}

	[Test]
	public async Task Other_widget_types_and_views_are_declined()
	{
		await using var plugin = await PluginFixture.StartAsync();

		var widget = await OpenAsync(plugin, UiSurfaceKinds.Widget, new() { [UiWidgetSurfaceAttributes.WidgetType] = Json("other.plugin::clock") });
		var dialog = await OpenAsync(plugin, UiSurfaceKinds.Dialog, new() { [UiDialogSurfaceAttributes.ViewId] = Json("something-else") });

		Assert.That(widget, Is.Null);
		Assert.That(dialog, Is.Null);
	}

	[Test]
	public void In_the_large_layout_the_first_control_pages_instead_of_completing_a_todo()
	{
		var large = LargeLayout(TodoWidgetSettings.Default);

		Assert.That(Controls(large).First().Key, Is.EqualTo("large-page"));
	}

	[Test]
	public void Without_complete_buttons_the_large_layout_leaves_the_tap_to_the_widget()
	{
		var large = LargeLayout(TodoWidgetSettings.Default with { Interactive = false });

		Assert.That(Controls(large), Is.Empty);
	}

	[Test]
	public void The_small_and_medium_layouts_have_no_controls()
	{
		var root = (UiResponsive)TodoWidgetView.Build(TodoSamples.State(), TodoWidgetSettings.Default, new NoCommands());
		var small = Controls(root.Default);
		var medium = root.Variants.Skip(1).SelectMany(v => Controls(v.Content));

		Assert.That(small.Concat(medium), Is.Empty);
	}

	[Test]
	public void No_text_in_a_layout_row_sits_beside_a_filling_sibling_without_its_own_slot()
	{
		var root = (UiResponsive)TodoWidgetView.Build(TodoSamples.State(), TodoWidgetSettings.Default, new NoCommands());
		var rows = root.Variants.Select(v => v.Content).Prepend(root.Default).SelectMany(Descendants).OfType<UiStack>()
			.Where(stack => stack.Direction == UiComponentDirections.Horizontal)
			.ToList();

		Assert.That(rows, Is.Not.Empty);
		foreach (var row in rows.Where(row => row.Children.Any(Fills)))
		{
			var unslotted = row.Children.OfType<UiTextRun>().Where(text => text.Fill != true && text.MainSize == default);
			Assert.That(unslotted.Select(text => text.Key), Is.Empty, $"row '{row.Key}'");
		}
	}

	[Test]
	public void Widget_settings_read_saved_values_and_clamp_invalid_ones()
	{
		var data = JsonDocument.Parse("""{"maxVisible":99,"sort":"manual","showCompleted":true,"statusFilter":"overdue","density":"compact","interactive":false,"unknown":1}""").RootElement;

		var settings = TodoWidgetSettings.Read(data);

		Assert.That(settings, Is.EqualTo(new TodoWidgetSettings
		{
			MaxVisible = TodoWidgetSettings.MaxVisibleLimit,
			Sort = TodoSortOrder.Manual,
			ShowCompleted = true,
			StatusFilter = TodoStatusFilter.Overdue,
			Interactive = false,
		}));
		Assert.That(TodoWidgetSettings.Read(JsonDocument.Parse("[]").RootElement), Is.EqualTo(TodoWidgetSettings.Default));
	}

	[Test]
	public void The_default_widget_data_matches_the_default_settings() =>
		Assert.That(TodoWidgetSettings.Read(JsonDocument.Parse(TodoWidgetSettings.DefaultDataJson).RootElement), Is.EqualTo(TodoWidgetSettings.Default));

	[Test]
	public async Task An_unusable_data_directory_reports_failures_instead_of_crashing()
	{
		var blocker = Path.Combine(Path.GetTempPath(), "todolist-plugin-tests", Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.GetDirectoryName(blocker)!);
		await File.WriteAllTextAsync(blocker, "not a directory");
		await using var plugin = await PluginFixture.StartAsync(blocker);

		var create = await plugin.Harness.Actions.ExecuteAsync("create-todo", new Dictionary<string, object?> { ["title"] = "Lost" });
		var count = await plugin.Harness.Variables.GetAsync("open-count");

		Assert.That(create.Error?.Code, Is.EqualTo(ActionErrorCodes.ProviderError));
		Assert.That(count.Data!.Value.GetProperty("value").GetProperty("kind").GetString(), Is.EqualTo("unavailable"));
		File.Delete(blocker);
	}

	private static async Task<IUiSession?> OpenAsync(PluginFixture plugin, string kind, Dictionary<string, JsonElement> attributes)
	{
		var integration = plugin.Harness.Services.GetRequiredService<TodoIntegration>();
		return await integration.CreateSessionAsync(
			new UiSessionRequest
			{
				Surface = new UiSurface { Kind = kind, SessionMode = UiSessionModes.Shared, Attributes = attributes },
				UiModelVersion = 1,
			},
			CancellationToken.None);
	}

	private static JsonElement Json(string value) => JsonSerializer.SerializeToElement(value);

	private static UiElement LargeLayout(TodoWidgetSettings settings)
	{
		var root = (UiResponsive)TodoWidgetView.Build(TodoSamples.State(), settings, new NoCommands());
		return root.Variants.Single(v => v.MinWidth == 2 && v.MinHeight == 2).Content;
	}

	/// <summary>Elements that claim a gesture, in tree order (the order hardware decks use).</summary>
	private static IEnumerable<UiElement> Controls(UiElement element)
	{
		if (element.Events.Count > 0)
		{
			yield return element;
		}

		var children = element switch
		{
			UiContainer container => container.Children,
			_ => [],
		};

		foreach (var child in children.SelectMany(Controls))
		{
			yield return child;
		}
	}

	private static IEnumerable<UiElement> Descendants(UiElement element) => element switch
	{
		UiContainer container => container.Children.SelectMany(Descendants).Prepend(element),
		UiWhen conditional => Descendants(conditional.Content()).Prepend(element),
		_ => [element],
	};

	private static bool Fills(UiElement element) => element switch
	{
		UiComponentContainer container => container.Fill == true,
		UiComponentLeaf leaf => leaf.Fill == true,
		_ => false,
	};

	private sealed class NoCommands : ITodoCommands
	{
		public Task<MacroDeck.Localization.LocalizedText?> CreateAsync(TodoDraft draft, CancellationToken cancellationToken) => Task.FromResult<MacroDeck.Localization.LocalizedText?>(null);

		public Task<MacroDeck.Localization.LocalizedText?> UpdateAsync(Guid id, TodoDraft draft, CancellationToken cancellationToken) => Task.FromResult<MacroDeck.Localization.LocalizedText?>(null);

		public Task<MacroDeck.Localization.LocalizedText?> SetCompletedAsync(Guid id, bool completed, CancellationToken cancellationToken) => Task.FromResult<MacroDeck.Localization.LocalizedText?>(null);

		public Task<MacroDeck.Localization.LocalizedText?> DeleteAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<MacroDeck.Localization.LocalizedText?>(null);
	}
}
