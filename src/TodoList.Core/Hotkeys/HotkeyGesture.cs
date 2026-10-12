using System.Text;

namespace TodoList.Core.Hotkeys;

[Flags]
public enum HotkeyModifiers
{
	None = 0,
	Control = 1,
	Alt = 2,
	Shift = 4,

	/// <summary>Command on macOS, the Windows key on Windows.</summary>
	Meta = 8,
}

public enum HotkeyValidationError
{
	None,
	MissingModifier,
	OptionOnlyOnMac,
}

/// <summary>
/// A global shortcut such as <c>Ctrl+Alt+Shift+T</c>. The key is a letter, a digit, F1 to F24 or Space.
/// The text form is platform neutral (Alt is Option, Meta is Command or the Windows key).
/// </summary>
public sealed record HotkeyGesture(HotkeyModifiers Modifiers, string Key)
{
	/// <summary>
	/// Rarely bound by the OS or common apps on either platform. Ctrl+Alt alone is AltGr on many Windows
	/// keyboard layouts, and macOS refuses Option-only combinations, so all three modifiers are used.
	/// </summary>
	public static HotkeyGesture Default { get; } =
		new(HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift, "T");

	public static bool TryParse(string? text, out HotkeyGesture gesture)
	{
		gesture = Default;
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}

		var modifiers = HotkeyModifiers.None;
		string? key = null;
		foreach (var raw in text.Split('+', StringSplitOptions.TrimEntries))
		{
			var token = raw.ToUpperInvariant();
			var modifier = token switch
			{
				"CTRL" or "CONTROL" or "CTL" => HotkeyModifiers.Control,
				"ALT" or "OPTION" or "OPT" => HotkeyModifiers.Alt,
				"SHIFT" => HotkeyModifiers.Shift,
				"META" or "CMD" or "COMMAND" or "WIN" or "WINDOWS" or "SUPER" => HotkeyModifiers.Meta,
				_ => HotkeyModifiers.None,
			};

			if (modifier != HotkeyModifiers.None)
			{
				modifiers |= modifier;
				continue;
			}

			if (key is not null || !TryNormalizeKey(token, out var normalized))
			{
				return false;
			}

			key = normalized;
		}

		if (key is null)
		{
			return false;
		}

		gesture = new HotkeyGesture(modifiers, key);
		return true;
	}

	public static bool TryNormalizeKey(string token, out string key)
	{
		key = token.ToUpperInvariant();
		if (key.Length == 1 && key[0] is (>= 'A' and <= 'Z') or (>= '0' and <= '9'))
		{
			return true;
		}

		if (key == "SPACE")
		{
			key = "Space";
			return true;
		}

		if (key.Length is 2 or 3 && key[0] == 'F' && int.TryParse(key.AsSpan(1), out var number) && number is >= 1 and <= 24)
		{
			return true;
		}

		key = string.Empty;
		return false;
	}

	/// <summary>
	/// A global shortcut needs Control, Alt or Meta so it does not swallow normal typing. macOS also
	/// refuses Option and Option+Shift combinations since macOS 15.
	/// </summary>
	public HotkeyValidationError Validate(bool isMacOs)
	{
		if ((Modifiers & (HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Meta)) == 0)
		{
			return HotkeyValidationError.MissingModifier;
		}

		if (isMacOs && (Modifiers & ~HotkeyModifiers.Shift) == HotkeyModifiers.Alt)
		{
			return HotkeyValidationError.OptionOnlyOnMac;
		}

		return HotkeyValidationError.None;
	}

	public override string ToString()
	{
		var text = new StringBuilder();
		Append(HotkeyModifiers.Control, "Ctrl");
		Append(HotkeyModifiers.Alt, "Alt");
		Append(HotkeyModifiers.Shift, "Shift");
		Append(HotkeyModifiers.Meta, "Meta");
		return text.Append(Key).ToString();

		void Append(HotkeyModifiers modifier, string name)
		{
			if (Modifiers.HasFlag(modifier))
			{
				text.Append(name).Append('+');
			}
		}
	}

	/// <summary>The native display form: symbols on macOS, words on Windows.</summary>
	public string ToDisplayString(bool isMacOs)
	{
		if (!isMacOs)
		{
			return ToString().Replace("Meta+", "Win+", StringComparison.Ordinal);
		}

		var text = new StringBuilder();
		if (Modifiers.HasFlag(HotkeyModifiers.Control))
		{
			text.Append('⌃');
		}

		if (Modifiers.HasFlag(HotkeyModifiers.Alt))
		{
			text.Append('⌥');
		}

		if (Modifiers.HasFlag(HotkeyModifiers.Shift))
		{
			text.Append('⇧');
		}

		if (Modifiers.HasFlag(HotkeyModifiers.Meta))
		{
			text.Append('⌘');
		}

		return text.Append(Key).ToString();
	}
}
