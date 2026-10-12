using MacroDeck.Plugin.Hosting;
using Microsoft.Extensions.Options;

namespace TodoList.Plugin.Hosting;

/// <summary>
/// Resolves where user data lives. Macro Deck passes a data directory that survives updates and rollbacks
/// to installed plugins; a self-registering development run gets none, so it uses a per-user folder.
/// The database itself may live elsewhere, see <see cref="TodoList.Core.Storage.StorageLocation"/>.
/// </summary>
public sealed class TodoDataPaths(IOptions<TodoListOptions> options, IOptions<PluginHostOptions> hostOptions)
{
	public string DataDirectory { get; } =
		options.Value.DataDirectory
		?? hostOptions.Value.DataDirectory
		?? Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
			"SuchByte",
			"TodoListForMacroDeck",
			"development");

	public bool IsDevelopmentFallback =>
		options.Value.DataDirectory is null && hostOptions.Value.DataDirectory is null;

	public string DesktopLockPath => Path.Combine(DataDirectory, "desktop.lock");
}
