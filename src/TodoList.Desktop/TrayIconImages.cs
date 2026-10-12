using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace TodoList.Desktop;

/// <summary>
/// Draws the tray glyph at runtime (a rounded box with a check mark) so no binary image ships in the repository.
/// macOS gets a black template image that the menu bar tints; Windows gets a colored icon that reads on light
/// and dark taskbars.
/// </summary>
internal static class TrayIconImages
{
	private static readonly Color Accent = Color.FromRgb(0x2F, 0x6F, 0xEB);

	public static byte[] MacTemplatePng(int pixels) => Encode(Render(pixels, template: true));

	public static Bitmap WindowsIcon(int pixels) => Render(pixels, template: false);

	private static RenderTargetBitmap Render(int pixels, bool template)
	{
		var bitmap = new RenderTargetBitmap(new PixelSize(pixels, pixels), new Vector(96, 96));
		using var context = bitmap.CreateDrawingContext();
		var size = (double)pixels;
		var inset = size * 0.12;
		var box = new Rect(inset, inset, size - (2 * inset), size - (2 * inset));
		var radius = size * 0.22;
		var stroke = size * 0.1;

		if (template)
		{
			context.DrawRectangle(null, new Pen(Brushes.Black, stroke), box.Deflate(stroke / 2), radius, radius);
			DrawCheck(context, box, new Pen(Brushes.Black, stroke, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
		}
		else
		{
			context.DrawRectangle(new SolidColorBrush(Accent), null, box, radius, radius);
			DrawCheck(context, box, new Pen(Brushes.White, stroke * 1.1, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round));
		}

		return bitmap;
	}

	private static void DrawCheck(DrawingContext context, Rect box, Pen pen)
	{
		var geometry = new StreamGeometry();
		using (var figure = geometry.Open())
		{
			figure.BeginFigure(new Point(box.X + (box.Width * 0.27), box.Y + (box.Height * 0.53)), isFilled: false);
			figure.LineTo(new Point(box.X + (box.Width * 0.44), box.Y + (box.Height * 0.70)));
			figure.LineTo(new Point(box.X + (box.Width * 0.74), box.Y + (box.Height * 0.32)));
			figure.EndFigure(isClosed: false);
		}

		context.DrawGeometry(null, pen, geometry);
	}

	private static byte[] Encode(Bitmap bitmap)
	{
		using var stream = new MemoryStream();
		bitmap.Save(stream, PngBitmapEncoderOptions.Default);
		return stream.ToArray();
	}
}
