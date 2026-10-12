namespace TodoList.Plugin.Hosting;

public sealed class TodoListOptions
{
	/// <summary>
	/// Overrides where the database lives. Unset, the plugin data directory Macro Deck provides is used.
	/// </summary>
	public string? DataDirectory { get; set; }
}
