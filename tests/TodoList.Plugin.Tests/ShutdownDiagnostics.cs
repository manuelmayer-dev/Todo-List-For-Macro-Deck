using System.Diagnostics;
using MacroDeck.Plugin.Testing;
using NUnit.Framework;

namespace TodoList.Plugin.Tests;

/// <summary>
/// TEMPORARY: reproduces conformance check MDC0604 against the built plugin and prints everything the host saw
/// whenever the plugin is still live after the 4004 close. Runs only when TODO_DIAG_PLUGIN names the plugin dll.
/// </summary>
[TestFixture]
[Category("ShutdownDiagnostics")]
public sealed class ShutdownDiagnostics
{
	private const int Attempts = 12;

	[Test]
	public async Task Supervisor_shutdown_stops_the_plugin()
	{
		var assembly = Environment.GetEnvironmentVariable("TODO_DIAG_PLUGIN");
		if (string.IsNullOrEmpty(assembly))
		{
			Assert.Ignore("TODO_DIAG_PLUGIN is not set.");
		}

		var failures = 0;
		for (var attempt = 1; attempt <= Attempts; attempt++)
		{
			if (!await RunOnceAsync(assembly!, attempt))
			{
				failures++;
			}
		}

		await TestContext.Out.WriteLineAsync($"Failures: {failures}/{Attempts}");
		Assert.That(failures, Is.Zero);
	}

	private static async Task<bool> RunOnceAsync(string assembly, int attempt)
	{
		await using var host = await MacroDeckTestHost.StartAsync();
		var plugin = await host.LaunchAsync(PluginLaunchSpec.ForDotnet(assembly));
		await using var _ = plugin;
		var clock = Stopwatch.StartNew();
		await host.WaitForSessionAsync(TimeSpan.FromSeconds(30));
		var sessionAt = clock.Elapsed;

		await host.DisconnectAsync(4004);
		var closedAt = clock.Elapsed;
		PluginHealthReport health;
		do
		{
			await Task.Delay(100);
			health = await plugin.ProbeHealthAsync(TimeSpan.FromSeconds(2));
		}
		while (health.Live && clock.Elapsed - closedAt < TimeSpan.FromSeconds(15));

		var stopped = !health.Live;
		var output = TestContext.Out;
		await output.WriteLineAsync($"=== Attempt {attempt}: {(stopped ? "stopped" : "STILL LIVE")} after {(clock.Elapsed - closedAt).TotalSeconds:F1} s (session at {sessionAt.TotalSeconds:F1} s, exited {plugin.HasExited})");
		if (stopped)
		{
			return true;
		}

		await output.WriteLineAsync($"Health: status {health.Status}, ready {health.Ready}, mode {health.Mode}");
		using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) })
		{
			try
			{
				await output.WriteLineAsync("Diagnostics: " + await http.GetStringAsync(new Uri(plugin.BaseAddress, "/_macrodeck/diagnostics")));
			}
			catch (Exception exception)
			{
				await output.WriteLineAsync("Diagnostics failed: " + exception.Message);
			}
		}

		await output.WriteLineAsync($"Sessions opened: {host.Sessions.Count}, closes: {string.Join(", ", host.Messages.Closes.Select(c => $"{c.CloseCode}@{c.RecordedAt:HH:mm:ss.fff}"))}");
		foreach (var message in host.Messages.All)
		{
			await output.WriteLineAsync($"  msg {message.Direction} {message.Envelope.Type} {message.Envelope.CorrelationId}");
		}

		foreach (var log in host.Logs.Events)
		{
			await output.WriteLineAsync($"  log {log.Timestamp:HH:mm:ss.fff} {log.Level} {log.SourceContext}: {log.Message} {log.Exception?.Message}");
		}

		await output.WriteLineAsync("stdout:\n" + plugin.StandardOutput);
		await output.WriteLineAsync("stderr:\n" + plugin.StandardError);
		return false;
	}
}
