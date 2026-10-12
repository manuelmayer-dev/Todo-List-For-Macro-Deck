using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Serilog;
using Microsoft.Extensions.DependencyInjection;
using TodoList.Desktop;
using TodoList.Plugin.Hosting;

namespace TodoList.Plugin;

/// <summary>
/// Unlike the template's top-level program, the Macro Deck host runs on the thread pool here: macOS only
/// allows UI on the process main thread, which the desktop todo list needs for as long as the plugin runs.
/// </summary>
internal static class Program
{
	[STAThread]
	public static int Main(string[] args)
	{
		// Identity, description and icon come from manifest.json at the content root.
		var builder = MacroDeckPlugin.CreatePlugin(args)
			.UseMacroDeckLogging()
			.UseLocalization(Strings.LocalizationCatalog)
			.RegisterIntegration<TodoIntegration>();
		builder.Services.AddTodoList();
		var plugin = builder.Build();

		var pluginTask = Task.Run(() => plugin.RunAsync());

		// The first Macro Deck session gets the machine to itself; without Macro Deck the UI starts anyway.
		var initialized = plugin.Services.GetRequiredService<DesktopStartSignal>().Ready;
		Task.WaitAny(initialized, pluginTask, Task.Delay(TimeSpan.FromSeconds(5)));
		if (!pluginTask.IsCompleted)
		{
			plugin.Services.GetRequiredService<DesktopUiHost>().Run(pluginTask, args);
		}

		// The desktop UI ends with the plugin, or earlier if it could not start; the plugin decides the exit.
		pluginTask.GetAwaiter().GetResult();
		return 0;
	}
}
