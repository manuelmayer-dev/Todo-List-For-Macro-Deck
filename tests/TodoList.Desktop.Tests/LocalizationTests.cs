using System.Globalization;
using NUnit.Framework;
using TodoList.Desktop.Localization;

namespace TodoList.Desktop.Tests;

[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void Every_desktop_string_is_translated_to_german()
	{
		var catalog = DesktopStrings.LocalizationCatalog;

		Assert.That(catalog.KeysOf("de"), Is.EquivalentTo(catalog.KeysOf(catalog.DefaultCulture)));
	}

	[TestCase("de-DE", "Heute 18:00")]
	[TestCase("de-AT", "Heute 18:00")]
	[TestCase("en-US", "Today 18:00")]
	[TestCase("fr-FR", "Today 18:00")]
	public void The_ui_language_picks_the_translation_or_falls_back_to_english(string culture, string expected)
	{
		var previous = CultureInfo.CurrentUICulture;
		CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
		try
		{
			Assert.That(DesktopText.DueToday("18:00"), Is.EqualTo(expected));
		}
		finally
		{
			CultureInfo.CurrentUICulture = previous;
		}
	}

	[Test]
	[SetUICulture("de-DE")]
	public void Placeholders_are_filled_in_translations() =>
		Assert.That(DesktopText.OpenFooter(3), Is.EqualTo("3 offen"));
}
