using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace TodoList.Desktop.Views;

/// <summary>
/// The short appear animation of the popup and the quick-add window: a fade with a slight zoom that settles
/// from where the window comes from, like a macOS popover.
/// </summary>
internal static class Reveal
{
	private static readonly TimeSpan FadeDuration = TimeSpan.FromMilliseconds(120);
	private static readonly TimeSpan MoveDuration = TimeSpan.FromMilliseconds(200);
	private static readonly ITransform Settled = TransformOperations.Parse("scale(1) translateY(0px)");

	/// <param name="target">The window's content root.</param>
	/// <param name="origin">The point it grows from, relative to its bounds.</param>
	/// <param name="offsetY">How far it travels, in pixels; negative starts it higher.</param>
	public static void Play(Visual target, RelativePoint origin, double offsetY)
	{
		target.Transitions = null;
		target.Opacity = 0;
		target.RenderTransformOrigin = origin;
		target.RenderTransform = TransformOperations.Parse(FormattableString.Invariant($"scale(0.96) translateY({offsetY}px)"));
		target.Transitions =
		[
			new DoubleTransition { Property = Visual.OpacityProperty, Duration = FadeDuration, Easing = new CubicEaseOut() },
			new TransformOperationsTransition { Property = Visual.RenderTransformProperty, Duration = MoveDuration, Easing = new QuinticEaseOut() },
		];

		// Changed after the start values are applied, so the transitions run from them.
		Dispatcher.UIThread.Post(() =>
		{
			target.Opacity = 1;
			target.RenderTransform = Settled;
		}, DispatcherPriority.Render);
	}
}
