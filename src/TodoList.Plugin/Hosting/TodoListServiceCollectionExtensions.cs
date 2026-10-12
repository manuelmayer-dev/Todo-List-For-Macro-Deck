using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Serilog;
using TodoList.Core;
using TodoList.Core.Scheduling;
using TodoList.Core.Settings;
using TodoList.Core.Storage;
using TodoList.Desktop;
using TodoList.Plugin.Actions;
using TodoList.Plugin.Events;
using TodoList.Plugin.Ui;
using TodoList.Plugin.Variables;

namespace TodoList.Plugin.Hosting;

public static class TodoListServiceCollectionExtensions
{
	/// <summary>Registers the shared store, scheduling, Macro Deck collaborators and the desktop UI host.</summary>
	public static IServiceCollection AddTodoList(this IServiceCollection services, Action<TodoListOptions>? configure = null)
	{
		services.AddOptions<TodoListOptions>();
		if (configure is not null)
		{
			services.Configure(configure);
		}

		services.TryAddSingleton(TimeProvider.System);
		services.AddSingleton<TodoDataPaths>();
		services.AddSingleton(provider => new StorageLocation(
			provider.GetRequiredService<TodoDataPaths>().DataDirectory,
			provider.GetRequiredService<ILogger>()));
		services.AddSingleton(provider => new TodoDatabase(
			StorageLocation.DatabasePathIn(provider.GetRequiredService<StorageLocation>().StartupDirectory),
			provider.GetRequiredService<ILogger>()));
		services.AddSingleton<TodoService>();
		services.AddSingleton<SettingsService>();
		services.AddSingleton<DataLocationService>();
		services.AddSingleton<ITransitionDeliveryGate, ConnectionDeliveryGate>();
		services.AddSingleton<DueMonitor>();
		services.AddSingleton<MinuteClock>();
		services.AddSingleton<TodoRuntime>();
		services.AddSingleton<DesktopStartSignal>();
		services.AddHostedService(provider => provider.GetRequiredService<TodoRuntime>());

		services.AddSingleton<TodoEventBridge>();
		services.AddSingleton<TodoVariables>();
		services.AddSingleton<TodoActionSupport>();
		services.AddSingleton<TodoUiBinder>();
		services.AddSingleton<ITodoCommands, TodoCommands>();

		services.AddSingleton(provider => new DesktopUiHost(
			provider.GetRequiredService<TodoService>(),
			provider.GetRequiredService<SettingsService>(),
			provider.GetRequiredService<DataLocationService>(),
			provider.GetRequiredService<TimeProvider>(),
			provider.GetRequiredService<MinuteClock>(),
			provider.GetRequiredService<TodoRuntime>().Ready,
			provider.GetRequiredService<TodoDataPaths>().DesktopLockPath,
			provider.GetRequiredService<ILogger>()));
		services.AddSingleton<IDesktopUi>(provider => provider.GetRequiredService<DesktopUiHost>());
		return services;
	}
}
