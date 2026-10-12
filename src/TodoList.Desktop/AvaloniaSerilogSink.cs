using Avalonia.Logging;

namespace TodoList.Desktop;

/// <summary>
/// Forwards Avalonia's own warnings and errors into the plugin log.
/// </summary>
internal sealed class AvaloniaSerilogSink(Serilog.ILogger logger) : ILogSink
{
	private readonly Serilog.ILogger _logger = logger.ForContext("SourceContext", "Avalonia");

	public bool IsEnabled(LogEventLevel level, string area) => level >= LogEventLevel.Warning;

	public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
		Log(level, area, source, messageTemplate, []);

	public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues)
	{
		if (!IsEnabled(level, area))
		{
			return;
		}

		var serilogLevel = level == LogEventLevel.Warning ? Serilog.Events.LogEventLevel.Warning : Serilog.Events.LogEventLevel.Error;
		_logger.Write(serilogLevel, "[{Area}] " + messageTemplate, [area, .. propertyValues]);
	}
}
