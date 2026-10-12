using NUnit.Framework;
using TodoList.Core.Hotkeys;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class HotkeyGestureTests
{
	private static HotkeyGesture Gesture(string text)
	{
		Assert.That(HotkeyGesture.TryParse(text, out var gesture), Is.True, text);
		return gesture;
	}

	[TestCase("Ctrl+Alt+Shift+T", "Ctrl+Alt+Shift+T")]
	[TestCase("control + option + k", "Ctrl+Alt+K")]
	[TestCase("Cmd+Shift+Space", "Shift+Meta+Space")]
	[TestCase("win+f12", "Meta+F12")]
	public void Parses_and_normalizes(string text, string expected)
	{
		Assert.That(HotkeyGesture.TryParse(text, out var gesture), Is.True);
		Assert.That(gesture.ToString(), Is.EqualTo(expected));
	}

	[TestCase("")]
	[TestCase("Ctrl+Alt")]
	[TestCase("Ctrl+A+B")]
	[TestCase("Ctrl+F25")]
	[TestCase("Ctrl+Enter")]
	public void Rejects_incomplete_or_unsupported_gestures(string text) =>
		Assert.That(HotkeyGesture.TryParse(text, out _), Is.False);

	[Test]
	public void A_gesture_without_control_alt_or_meta_is_invalid()
	{
		var gesture = Gesture("Shift+T");

		Assert.That(gesture.Validate(isMacOs: false), Is.EqualTo(HotkeyValidationError.MissingModifier));
	}

	[TestCase("Alt+T", true, HotkeyValidationError.OptionOnlyOnMac)]
	[TestCase("Alt+Shift+T", true, HotkeyValidationError.OptionOnlyOnMac)]
	[TestCase("Alt+Shift+T", false, HotkeyValidationError.None)]
	[TestCase("Ctrl+Alt+T", true, HotkeyValidationError.None)]
	public void Option_only_gestures_are_refused_on_macOS(string text, bool isMac, HotkeyValidationError expected)
	{
		var gesture = Gesture(text);

		Assert.That(gesture.Validate(isMac), Is.EqualTo(expected));
	}

	[Test]
	public void Display_uses_symbols_on_macOS_and_words_on_Windows()
	{
		var gesture = Gesture("Ctrl+Alt+Shift+Meta+T");

		Assert.Multiple(() =>
		{
			Assert.That(gesture.ToDisplayString(isMacOs: true), Is.EqualTo("⌃⌥⇧⌘T"));
			Assert.That(gesture.ToDisplayString(isMacOs: false), Is.EqualTo("Ctrl+Alt+Shift+Win+T"));
		});
	}
}
