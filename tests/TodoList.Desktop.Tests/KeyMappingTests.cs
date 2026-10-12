using Avalonia.Input;
using NUnit.Framework;
using TodoList.Core.Hotkeys;
using TodoList.Desktop.Native.MacOS;
using TodoList.Desktop.Native.Windows;
using TodoList.Desktop.Views;

namespace TodoList.Desktop.Tests;

[TestFixture]
public sealed class KeyMappingTests
{
	[TestCase("T", 0x54u)]
	[TestCase("7", 0x37u)]
	[TestCase("Space", 0x20u)]
	[TestCase("F1", 0x70u)]
	[TestCase("F24", 0x87u)]
	public void Windows_virtual_keys_match_the_documented_codes(string key, uint expected)
	{
		Assert.That(Win32HotkeyWindow.TryGetVirtualKey(key, out var code), Is.True);
		Assert.That(code, Is.EqualTo(expected));
	}

	[TestCase("T", 0x11u)]
	[TestCase("A", 0x00u)]
	[TestCase("Space", 0x31u)]
	[TestCase("F12", 0x6Fu)]
	public void Mac_key_codes_match_the_ansi_layout(string key, uint expected)
	{
		Assert.That(MacKeyCodes.TryGet(key, out var code), Is.True);
		Assert.That(code, Is.EqualTo(expected));
	}

	[Test]
	public void Every_key_a_gesture_accepts_maps_on_windows()
	{
		var keys = Enumerable.Range('A', 26).Select(c => ((char)c).ToString())
			.Concat(Enumerable.Range(0, 10).Select(d => d.ToString(System.Globalization.CultureInfo.InvariantCulture)))
			.Concat(Enumerable.Range(1, 24).Select(f => $"F{f}"))
			.Append("Space");

		foreach (var key in keys)
		{
			Assert.That(HotkeyGesture.TryNormalizeKey(key, out var normalized), Is.True, key);
			Assert.That(Win32HotkeyWindow.TryGetVirtualKey(normalized, out _), Is.True, key);
		}
	}

	[Test]
	public void Every_letter_and_digit_maps_on_macos()
	{
		foreach (var key in Enumerable.Range('A', 26).Select(c => ((char)c).ToString()).Concat(Enumerable.Range(0, 10).Select(d => d.ToString(System.Globalization.CultureInfo.InvariantCulture))))
		{
			Assert.That(MacKeyCodes.TryGet(key, out _), Is.True, key);
		}
	}

	[TestCase(Key.T, "T")]
	[TestCase(Key.D5, "5")]
	[TestCase(Key.F9, "F9")]
	[TestCase(Key.Space, "Space")]
	public void The_shortcut_recorder_maps_avalonia_keys_to_gesture_keys(Key key, string expected)
	{
		Assert.That(SettingsWindow.TryMapKey(key, out var token), Is.True);
		Assert.That(token, Is.EqualTo(expected));
	}

	[TestCase(Key.LeftCtrl)]
	[TestCase(Key.Enter)]
	[TestCase(Key.OemComma)]
	public void The_shortcut_recorder_ignores_keys_a_gesture_cannot_use(Key key) =>
		Assert.That(SettingsWindow.TryMapKey(key, out _), Is.False);
}
