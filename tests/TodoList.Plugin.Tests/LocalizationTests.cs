using NUnit.Framework;

namespace TodoList.Plugin.Tests;

/// <summary>
/// Guards the localization wiring rather than any wording: a missing catalog registration leaves every label
/// showing its raw key.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id() =>
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.suchbyte.todo-list-plugin"));

	[Test]
	public void English_is_the_default_culture()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Does.Contain("en"));
	}

	[Test]
	public void Every_key_the_default_culture_declares_resolves_to_text()
	{
		foreach (var key in Strings.LocalizationCatalog.KeysOf("en"))
		{
			Assert.That(Strings.LocalizationCatalog.TryGetTemplate("en", key, out var text), Is.True, key);
			Assert.That(text, Is.Not.Empty, key);
		}
	}

	[Test]
	public void German_translates_every_key() =>
		Assert.That(Strings.LocalizationCatalog.KeysOf("de"), Is.EquivalentTo(Strings.LocalizationCatalog.KeysOf("en")));

	[Test]
	public void A_regional_culture_falls_back_to_its_language()
	{
		var registry = new MacroDeck.Localization.LocalizationCatalogRegistry();
		registry.Register(Strings.LocalizationCatalog);
		var resolver = new MacroDeck.Localization.LocalizationResolver(registry);

		Assert.Multiple(() =>
		{
			Assert.That(resolver.Resolve(Strings.Widget.OverdueCount("2"), "de-CH"), Is.EqualTo("2 überfällig"));
			Assert.That(resolver.Resolve(Strings.Widget.OverdueCount("2"), "ja-JP"), Is.EqualTo("2 overdue"));
		});
	}
}
