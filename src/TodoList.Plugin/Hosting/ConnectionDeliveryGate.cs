using MacroDeck.Plugin.Hosting.Transport;
using TodoList.Core.Scheduling;

namespace TodoList.Plugin.Hosting;

/// <summary>
/// Open while a Macro Deck session is connected. Events published without one are dropped by the SDK, so due
/// transitions wait for the connection instead of being recorded and lost.
/// </summary>
public sealed class ConnectionDeliveryGate(PluginConnectionState connection) : ITransitionDeliveryGate
{
	public bool IsOpen => connection.IsReady;
}
