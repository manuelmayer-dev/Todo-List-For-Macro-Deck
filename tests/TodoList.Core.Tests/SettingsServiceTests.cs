using NUnit.Framework;
using TodoList.Core.Hotkeys;
using TodoList.Core.Settings;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class SettingsServiceTests
{
	[Test]
	public async Task Defaults_apply_when_nothing_was_saved()
	{
		await using var store = await TestStore.CreateAsync();

		Assert.That(store.Settings.Current, Is.EqualTo(new TodoSettings()));
		Assert.That(store.Settings.Current.Hotkey, Is.EqualTo("Ctrl+Alt+Shift+T"));
	}

	[Test]
	public async Task Saved_settings_survive_a_restart_and_raise_changed()
	{
		await using var store = await TestStore.CreateAsync();
		TodoSettings? raised = null;
		store.Settings.Changed += (_, s) => raised = s;
		var wanted = new TodoSettings { Theme = ThemePreference.Dark, DueSoonMinutes = 15, HotkeyEnabled = false, Hotkey = "cmd+shift+space" };

		await store.Settings.SaveAsync(wanted, CancellationToken.None);
		await store.ReopenAsync();

		Assert.Multiple(() =>
		{
			Assert.That(store.Settings.Current.Theme, Is.EqualTo(ThemePreference.Dark));
			Assert.That(store.Settings.Current.Thresholds.DueSoon, Is.EqualTo(TimeSpan.FromMinutes(15)));
			Assert.That(store.Settings.Current.HotkeyEnabled, Is.False);
			Assert.That(store.Settings.Current.Hotkey, Is.EqualTo("Shift+Meta+Space"));
			Assert.That(raised, Is.Not.Null);
		});
	}

	[Test]
	public void Out_of_range_values_are_clamped_and_bad_hotkeys_reset()
	{
		var normalized = new TodoSettings { DueSoonMinutes = -5, OverdueGraceMinutes = 999_999, Hotkey = "Ctrl+Nope" }.Normalize();

		Assert.Multiple(() =>
		{
			Assert.That(normalized.DueSoonMinutes, Is.Zero);
			Assert.That(normalized.OverdueGraceMinutes, Is.EqualTo(TodoSettings.MaxThresholdMinutes));
			Assert.That(normalized.Hotkey, Is.EqualTo(HotkeyGesture.Default.ToString()));
		});
	}
}
