using System.Text.Json;
using MacroDeck.Plugin.Hosting.Transport;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Events;
using MacroDeck.Sdk.Ui;
using MacroDeck.Sdk.Variables;
using MacroDeck.Sdk.Widgets;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Model.Surfaces;
using MacroDeck.Ui.Runtime;
using Serilog;
using TodoList.Core.Scheduling;
using TodoList.Core.Settings;
using TodoList.Desktop;
using TodoList.Plugin.Actions;
using TodoList.Plugin.Events;
using TodoList.Plugin.Hosting;
using TodoList.Plugin.Ui;
using TodoList.Plugin.Variables;

namespace TodoList.Plugin;

/// <summary>
/// The plugin's integration: lifecycle and the Macro Deck capabilities. The work lives in the collaborators;
/// this class only wires them to the host.
/// </summary>
public sealed class TodoIntegration : IPluginIntegration, IEventProvider, IVariableProvider, IWidgetTypeProvider, IUiProvider
{
	public const string WidgetTypeLocalId = "todo-list";

	private readonly TodoRuntime _runtime;
	private readonly DueMonitor _monitor;
	private readonly TodoEventBridge _events;
	private readonly TodoVariables _variables;
	private readonly TodoUiBinder _ui;
	private readonly ITodoCommands _commands;
	private readonly PluginConnectionState _connection;
	private readonly DesktopStartSignal _desktopStart;
	private readonly ILogger _logger;
	private volatile string? _widgetTypeId;
	private int _watchingConnection;

	public TodoIntegration(
		TodoRuntime runtime,
		DueMonitor monitor,
		TodoEventBridge events,
		TodoVariables variables,
		TodoUiBinder ui,
		ITodoCommands commands,
		TodoActionSupport actions,
		SettingsService settings,
		IDesktopUi desktop,
		PluginConnectionState connection,
		DesktopStartSignal desktopStart,
		ILogger logger)
	{
		_runtime = runtime;
		_monitor = monitor;
		_events = events;
		_variables = variables;
		_ui = ui;
		_commands = commands;
		_connection = connection;
		_desktopStart = desktopStart;
		_logger = logger.ForContext<TodoIntegration>();
		Actions =
		[
			new CreateTodoAction(actions),
			new CompleteTodoAction(actions, settings),
			new ReopenTodoAction(actions),
			new DeleteTodoAction(actions),
			new OpenTodoListAction(),
			new OpenDesktopTodoListAction(desktop),
		];
	}

	public IReadOnlyList<IActionDefinition> Actions { get; }

	public IReadOnlyList<EventDefinition> EventDefinitions => TodoEvents.Definitions;

	public IReadOnlyList<VariableDefinition> Variables => TodoVariables.Definitions;

	public IReadOnlyList<UiSurfaceDeclaration> Surfaces { get; } =
	[
		new() { Kind = UiSurfaceKinds.Widget, SessionMode = UiSessionModes.Shared },
		new() { Kind = UiSurfaceKinds.Preview, SessionMode = UiSessionModes.Shared },
		new() { Kind = UiSurfaceKinds.Config, SessionMode = UiSessionModes.Exclusive },
		new() { Kind = UiSurfaceKinds.Dialog, SessionMode = UiSessionModes.Exclusive },
	];

	public async Task InitializeAsync(IIntegrationContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Runs again after every reconnect: attaching replaces the publisher and starting a running monitor
		// is a no-op, so nothing is subscribed twice.
		_events.Attach(context.Events);
		if (Interlocked.Exchange(ref _watchingConnection, 1) == 0)
		{
			// A resumed session does not initialize again, so held due transitions are released here.
			_connection.Connected += OnConnected;
		}

		if (await _runtime.Ready)
		{
			await _monitor.StartAsync(CancellationToken.None);
			_logger.Information("Todo list initialized");
		}
		else
		{
			_logger.Warning("Todo list initialized without a usable database");
		}

		_desktopStart.StepCompleted();
	}

	private void OnConnected(object? sender, PluginConnectedEventArgs e) => _ = Task.Run(async () =>
	{
		try
		{
			await _monitor.ReconcileAsync(CancellationToken.None);
		}
		catch (OperationCanceledException)
		{
			// A fresh session stops the monitor while re-initializing; InitializeAsync reconciles instead.
		}
		catch (Exception exception)
		{
			_logger.Error(exception, "Checking due todos after reconnecting failed");
		}
	});

	public Task ShutdownAsync()
	{
		_monitor.Stop();
		_events.Detach();
		return Task.CompletedTask;
	}

	public ValueTask<VariableReading> ReadAsync(string localId, CancellationToken cancellationToken = default) =>
		ValueTask.FromResult(_runtime.Ready is { IsCompletedSuccessfully: true, Result: true }
			? _variables.Read(localId)
			: VariableReading.Unavailable);

	public async Task InitializeAsync(IWidgetTypeProviderContext context, CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(context);
		try
		{
			var registration = await context.RegisterWidgetTypeAsync(Descriptor, cancellationToken);
			_widgetTypeId = registration.WidgetTypeId;
		}
		finally
		{
			_desktopStart.StepCompleted();
		}
	}

	public IReadOnlyList<WidgetTypeDescriptor> GetWidgetTypes() => [Descriptor];

	public Task<IUiSession?> CreateSessionAsync(UiSessionRequest request, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		var surface = request.Surface;
		IUiSession? session = surface.Kind switch
		{
			UiSurfaceKinds.Widget or UiSurfaceKinds.Preview when IsOurWidget(surface, UiWidgetSurfaceAttributes.WidgetType) =>
				WidgetSession(surface),
			UiSurfaceKinds.Config when IsOurWidget(surface, UiConfigSurfaceAttributes.WidgetType) &&
				Attribute(surface, UiConfigSurfaceAttributes.EntryPoint) == UiConfigEntryPoints.WidgetConfig =>
				new ViewSession(new UiView(surface, TodoWidgetConfigView.Build(ReadSettings(surface, UiConfigSurfaceAttributes.WidgetData))), lease: null),
			UiSurfaceKinds.Dialog when Attribute(surface, UiDialogSurfaceAttributes.ViewId) == TodoModalView.ViewId =>
				new ViewSession(new UiView(surface, TodoModalView.Build(_ui.State, _commands)), _ui.TrackView()),
			_ => null,
		};

		return Task.FromResult(session);
	}

	internal static WidgetTypeDescriptor Descriptor { get; } = new(
		WidgetTypeLocalId,
		Strings.Widget.Name(),
		Strings.Widget.Description(),
		DefaultData: TodoWidgetSettings.DefaultDataJson,
		DataSchema: TodoWidgetSettings.DataSchemaJson,
		HasConfiguration: true)
	{
		DefaultShortPressAction = new WidgetDefaultAction(OpenTodoListAction.ActionId),
	};

	private ViewSession WidgetSession(UiSurface surface)
	{
		var settings = ReadSettings(surface, UiWidgetSurfaceAttributes.Data);

		// The picker shows a sample so nothing live is read while someone browses widget types.
		if (surface.Attributes.TryGetValue(UiWidgetSurfaceAttributes.Sample, out var sample) && sample.ValueKind == JsonValueKind.True)
		{
			return new ViewSession(new UiView(surface, TodoWidgetView.Build(TodoSamples.State(), settings, _commands)), lease: null);
		}

		return new ViewSession(new UiView(surface, TodoWidgetView.Build(_ui.State, settings, _commands)), _ui.TrackView());
	}

	private bool IsOurWidget(UiSurface surface, string attribute) =>
		Attribute(surface, attribute) is { } type &&
		(type == _widgetTypeId || type.EndsWith("::" + WidgetTypeLocalId, StringComparison.Ordinal));

	private static string? Attribute(UiSurface surface, string name) =>
		surface.Attributes.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

	private static TodoWidgetSettings ReadSettings(UiSurface surface, string attribute) =>
		surface.Attributes.TryGetValue(attribute, out var data) ? TodoWidgetSettings.Read(data) : TodoWidgetSettings.Default;
}
