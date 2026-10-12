using System.Text.Json;
using Serilog;

namespace TodoList.Core.Storage;

/// <summary>
/// Remembers a user-chosen data folder in a small file inside the default folder. It cannot live in the
/// settings, because those are stored in the database that moves with it.
/// </summary>
public sealed class StorageLocation
{
	public const string DatabaseFileName = "todos.db";
	private const string PointerFileName = "storage-location.json";

	private readonly ILogger _logger;

	public StorageLocation(string defaultDirectory, ILogger logger)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(defaultDirectory);
		DefaultDirectory = Path.GetFullPath(defaultDirectory);
		_logger = logger.ForContext<StorageLocation>();
		CustomDirectory = Read();
	}

	public string DefaultDirectory { get; }

	/// <summary>The folder the user picked, or null for the default.</summary>
	public string? CustomDirectory { get; private set; }

	/// <summary>
	/// Where the database is opened at startup. A custom folder that is gone (an unplugged drive, a deleted
	/// sync folder) falls back to the default, so the plugin keeps working and the setting shows the problem.
	/// </summary>
	public string StartupDirectory
	{
		get
		{
			if (CustomDirectory is { } custom && !Directory.Exists(custom))
			{
				_logger.Warning("The chosen data folder {Directory} is missing; using {Default}", custom, DefaultDirectory);
				return DefaultDirectory;
			}

			return CustomDirectory ?? DefaultDirectory;
		}
	}

	public static string DatabasePathIn(string directory) => Path.Combine(directory, DatabaseFileName);

	/// <summary>Stores <paramref name="directory"/>; null or the default folder clears the choice.</summary>
	public void Save(string? directory)
	{
		var normalized = directory is null ? null : Path.GetFullPath(directory);
		if (normalized is not null && SamePath(normalized, DefaultDirectory))
		{
			normalized = null;
		}

		var pointer = Path.Combine(DefaultDirectory, PointerFileName);
		if (normalized is null)
		{
			File.Delete(pointer);
		}
		else
		{
			Directory.CreateDirectory(DefaultDirectory);
			var temporary = pointer + ".tmp";
			File.WriteAllText(temporary, JsonSerializer.Serialize(new Pointer(normalized)));
			File.Move(temporary, pointer, overwrite: true);
		}

		CustomDirectory = normalized;
	}

	internal static bool SamePath(string left, string right) =>
		string.Equals(
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
			Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
			OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

	private string? Read()
	{
		var pointer = Path.Combine(DefaultDirectory, PointerFileName);
		try
		{
			if (!File.Exists(pointer))
			{
				return null;
			}

			var directory = JsonSerializer.Deserialize<Pointer>(File.ReadAllText(pointer))?.Directory;
			return string.IsNullOrWhiteSpace(directory) ? null : Path.GetFullPath(directory);
		}
		catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
		{
			_logger.Warning(exception, "The data folder setting at {Path} is unreadable; using the default", pointer);
			return null;
		}
	}

	private sealed record Pointer(string Directory);
}
