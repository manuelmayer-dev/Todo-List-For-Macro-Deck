using Avalonia;
using NUnit.Framework;

namespace TodoList.Desktop.Tests;

[TestFixture]
public sealed class PopupPlacementTests
{
	private const int Gap = 6;
	private static readonly PixelSize Popup = new(360, 480);
	private static readonly PixelRect Screen = new(0, 0, 1920, 1080);

	[Test]
	public void A_bottom_taskbar_puts_the_popup_above_the_icon()
	{
		var work = new PixelRect(0, 0, 1920, 1040);
		var icon = new PixelRect(1700, 1050, 24, 24);

		var position = PopupPlacement.Compute(icon, Popup, Screen, work, Gap);

		Assert.That(position, Is.EqualTo(new PixelPoint(1700 + 12 - 180, 1040 - 480 - Gap)));
	}

	[Test]
	public void A_menu_bar_puts_the_popup_below_the_item_and_inside_the_screen()
	{
		var work = new PixelRect(0, 25, 1512, 957);
		var item = new PixelRect(1480, 0, 24, 24);

		var position = PopupPlacement.Compute(item, Popup, new PixelRect(0, 0, 1512, 982), work, Gap);

		Assert.Multiple(() =>
		{
			Assert.That(position.Y, Is.EqualTo(25 + Gap));
			Assert.That(position.X + Popup.Width, Is.LessThanOrEqualTo(1512 - Gap));
		});
	}

	[Test]
	public void A_left_taskbar_puts_the_popup_to_the_right_of_the_icon()
	{
		var work = new PixelRect(48, 0, 1872, 1080);
		var icon = new PixelRect(12, 900, 24, 24);

		var position = PopupPlacement.Compute(icon, Popup, Screen, work, Gap);

		Assert.That(position.X, Is.EqualTo(48 + Gap));
		Assert.That(position.Y + Popup.Height, Is.LessThanOrEqualTo(1080 - Gap));
	}

	[Test]
	public void A_right_taskbar_puts_the_popup_to_the_left_of_the_icon()
	{
		var work = new PixelRect(0, 0, 1872, 1080);
		var icon = new PixelRect(1884, 500, 24, 24);

		var position = PopupPlacement.Compute(icon, Popup, Screen, work, Gap);

		Assert.That(position, Is.EqualTo(new PixelPoint(1872 - 360 - Gap, 512 - 240)));
	}

	[Test]
	public void An_auto_hidden_taskbar_is_inferred_from_the_icon_position()
	{
		var icon = new PixelRect(1700, 1060, 20, 20);

		var position = PopupPlacement.Compute(icon, Popup, Screen, Screen, Gap);

		Assert.That(position.Y, Is.EqualTo(1060 - 480 - Gap));
	}

	[Test]
	public void A_second_monitor_with_negative_coordinates_keeps_the_popup_on_that_monitor()
	{
		var monitor = new PixelRect(-2560, 0, 2560, 1440);
		var work = new PixelRect(-2560, 0, 2560, 1400);
		var icon = new PixelRect(-30, 1410, 24, 24);

		var position = PopupPlacement.Compute(icon, Popup, monitor, work, Gap);

		Assert.Multiple(() =>
		{
			Assert.That(position.X, Is.GreaterThanOrEqualTo(-2560 + Gap));
			Assert.That(position.X + Popup.Width, Is.LessThanOrEqualTo(-Gap));
			Assert.That(position.Y + Popup.Height, Is.EqualTo(1400 - Gap));
		});
	}

	[Test]
	public void Without_an_anchor_the_popup_goes_to_the_tray_corner()
	{
		var work = new PixelRect(0, 0, 1920, 1040);

		var position = PopupPlacement.Compute(null, Popup, Screen, work, Gap);

		Assert.That(position, Is.EqualTo(new PixelPoint(1920 - 360 - Gap, 1040 - 480 - Gap)));
	}
}
