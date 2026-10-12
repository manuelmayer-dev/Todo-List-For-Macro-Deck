using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using TodoList.Desktop.Views;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;

namespace TodoList.Desktop.Theme;

/// <summary>
/// The desktop look: Fluent's controls restyled after macOS and launcher apps like Raycast. Translucent
/// surfaces, hairlines, 13 point system text, the system accent colour, subtle selections instead of
/// coloured bars. Button classes: <c>primary</c>, <c>plain</c>, <c>icon</c>; TextBox class <c>bare</c>.
/// </summary>
internal static class AppTheme
{
	public const double BaseFontSize = 13;
	public static readonly CornerRadius ControlRadius = new(7);
	public static readonly CornerRadius RowRadius = new(8);
	public static readonly CornerRadius WindowRadius = new(12);
	public static readonly CornerRadius LauncherRadius = new(14);

	public static void Apply(Application application)
	{
		var accent = SystemAccent(application);
		var fluent = new FluentTheme();
		fluent.Palettes[ThemeVariant.Dark] = Palette(Tokens.Dark, accent ?? Tokens.Dark.DefaultAccent);
		fluent.Palettes[ThemeVariant.Light] = Palette(Tokens.Light, accent ?? Tokens.Light.DefaultAccent);
		application.Styles.Add(fluent);

		application.Resources.ThemeDictionaries[ThemeVariant.Dark] = Brushes(Tokens.Dark, accent ?? Tokens.Dark.DefaultAccent);
		application.Resources.ThemeDictionaries[ThemeVariant.Light] = Brushes(Tokens.Light, accent ?? Tokens.Light.DefaultAccent);
		application.Resources["ControlCornerRadius"] = ControlRadius;
		application.Resources["OverlayCornerRadius"] = RowRadius;

		foreach (var style in ControlStyles())
		{
			application.Styles.Add(style);
		}
	}

	// The accent the user picked in the system settings, so the popup matches the rest of the desktop.
	private static Color? SystemAccent(Application application)
	{
		var accent = application.PlatformSettings?.GetColorValues().AccentColor1;
		return accent is { A: > 0 } color ? color : null;
	}

	private static ColorPaletteResources Palette(Tokens t, Color accent) => new()
	{
		Accent = accent,
		RegionColor = t.Surface,
		AltHigh = t.Surface,
		BaseHigh = t.LabelPrimary,
		BaseMediumHigh = t.LabelPrimary,
		BaseMedium = t.LabelSecondary,
		BaseMediumLow = t.LabelTertiary,
		BaseLow = t.Separator,
		ChromeMedium = t.Control,
		ChromeMediumLow = t.Surface,
		ChromeLow = t.Surface,
		ChromeHigh = t.Separator,
		ListLow = t.Selection,
		ListMedium = t.Selection,
		ErrorText = t.Red,
	};

	private static ResourceDictionary Brushes(Tokens t, Color accent) => new()
	{
		[Keys.Window] = new SolidColorBrush(t.Window),
		[Keys.Surface] = new SolidColorBrush(t.Surface),
		[Keys.Control] = new SolidColorBrush(t.Control),
		[Keys.ControlHover] = new SolidColorBrush(t.ControlHover),
		[Keys.Selection] = new SolidColorBrush(t.Selection),
		[Keys.Separator] = new SolidColorBrush(t.Separator),
		[Keys.Keycap] = new SolidColorBrush(t.Keycap),
		[Keys.LabelPrimary] = new SolidColorBrush(t.LabelPrimary),
		[Keys.LabelSecondary] = new SolidColorBrush(t.LabelSecondary),
		[Keys.LabelTertiary] = new SolidColorBrush(t.LabelTertiary),
		[Keys.Red] = new SolidColorBrush(t.Red),
		[Keys.RedTint] = new SolidColorBrush(t.RedTint),
		[Keys.Orange] = new SolidColorBrush(t.Orange),
		[Keys.OrangeTint] = new SolidColorBrush(t.OrangeTint),
		[Keys.Green] = new SolidColorBrush(t.Green),
		[Keys.SwitchOff] = new SolidColorBrush(t.SwitchOff),
		[Keys.Accent] = new SolidColorBrush(accent),
		[Keys.AccentTint] = new SolidColorBrush(Color.FromArgb(0x33, accent.R, accent.G, accent.B)),
		[Keys.OnAccent] = new SolidColorBrush(Colors.White),
		[Keys.FocusRing] = new SolidColorBrush(Color.FromArgb(0x99, accent.R, accent.G, accent.B)),
		// Popups float over other windows, so their surfaces are opaque; hovered entries take the accent
		// with white text like macOS menus.
		["ComboBoxDropDownBackground"] = new SolidColorBrush(t.Surface),
		["ComboBoxDropDownBorderBrush"] = new SolidColorBrush(t.Separator),
		["ComboBoxItemBackgroundPointerOver"] = new SolidColorBrush(accent),
		["ComboBoxItemForegroundPointerOver"] = new SolidColorBrush(Colors.White),
		["ComboBoxItemBackgroundPressed"] = new SolidColorBrush(accent),
		["ComboBoxItemForegroundPressed"] = new SolidColorBrush(Colors.White),
		["ComboBoxItemBackgroundSelected"] = new SolidColorBrush(t.Selection),
		["ComboBoxItemForegroundSelected"] = new SolidColorBrush(t.LabelPrimary),
		["ComboBoxItemBackgroundSelectedPointerOver"] = new SolidColorBrush(accent),
		["ComboBoxItemForegroundSelectedPointerOver"] = new SolidColorBrush(Colors.White),
		["ComboBoxItemBackgroundSelectedPressed"] = new SolidColorBrush(accent),
		["ComboBoxItemForegroundSelectedPressed"] = new SolidColorBrush(Colors.White),
		["MenuFlyoutPresenterBackground"] = new SolidColorBrush(t.Surface),
		["MenuFlyoutPresenterBorderBrush"] = new SolidColorBrush(t.Separator),
		["MenuFlyoutItemBackgroundPointerOver"] = new SolidColorBrush(accent),
		["MenuFlyoutItemForegroundPointerOver"] = new SolidColorBrush(Colors.White),
		["MenuFlyoutItemBackgroundPressed"] = new SolidColorBrush(accent),
		["MenuFlyoutItemForegroundPressed"] = new SolidColorBrush(Colors.White),
		["FlyoutPresenterBackground"] = new SolidColorBrush(t.Surface),
		// The date picker's calendar: flat and quiet like the macOS one, out-of-month days only dimmed.
		["CalendarViewBackground"] = new SolidColorBrush(t.Surface),
		["CalendarViewBorderBrush"] = Avalonia.Media.Brushes.Transparent,
		["CalendarViewCalendarItemRevealBackground"] = Avalonia.Media.Brushes.Transparent,
		["CalendarViewCalendarItemRevealBorderBrush"] = Avalonia.Media.Brushes.Transparent,
		["CalendarViewOutOfScopeBackground"] = Avalonia.Media.Brushes.Transparent,
		["CalendarViewOutOfScopeForeground"] = new SolidColorBrush(t.LabelTertiary),
		["CalendarViewNavigationButtonBackground"] = Avalonia.Media.Brushes.Transparent,
		["CalendarViewNavigationButtonBorderBrush"] = Avalonia.Media.Brushes.Transparent,
		["CalendarViewHoverBorderBrush"] = new SolidColorBrush(t.ControlHover),
		["CalendarViewPressedBorderBrush"] = new SolidColorBrush(accent),
		["CalendarViewSelectedBorderBrush"] = new SolidColorBrush(accent),
		["CalendarViewSelectedHoverBorderBrush"] = new SolidColorBrush(accent),
		["CalendarViewSelectedPressedBorderBrush"] = new SolidColorBrush(accent),
		["CalendarViewForeground"] = new SolidColorBrush(t.LabelPrimary),
		["CalendarViewCalendarItemForeground"] = new SolidColorBrush(t.LabelPrimary),
		["CalendarViewWeekDayForegroundDisabled"] = new SolidColorBrush(t.LabelSecondary),
		["TextControlPlaceholderForeground"] = new SolidColorBrush(t.LabelTertiary),
		["TextControlPlaceholderForegroundPointerOver"] = new SolidColorBrush(t.LabelTertiary),
		["TextControlPlaceholderForegroundFocused"] = new SolidColorBrush(t.LabelTertiary),
		["TextControlSelectionHighlightColor"] = new SolidColorBrush(Color.FromArgb(0x66, accent.R, accent.G, accent.B)),
	};

	private static DynamicResourceExtension Brush(string key) => new(key);

	private static FuncControlTemplate<CalendarDayButton> DayTemplate() => new((_, _) => new Border
	{
		CornerRadius = new CornerRadius(15),
		[!Border.BackgroundProperty] = new TemplateBinding(TemplatedControl.BackgroundProperty),
		Child = new ContentPresenter
		{
			// Not named like Fluent's part, so Fluent's day styles do not reach it.
			HorizontalAlignment = HorizontalAlignment.Center,
			VerticalAlignment = VerticalAlignment.Center,
			[!ContentPresenter.ContentProperty] = new TemplateBinding(ContentControl.ContentProperty),
			[!ContentPresenter.ForegroundProperty] = new TemplateBinding(TemplatedControl.ForegroundProperty),
			[!ContentPresenter.FontWeightProperty] = new TemplateBinding(TemplatedControl.FontWeightProperty),
			[!ContentPresenter.FontSizeProperty] = new TemplateBinding(TemplatedControl.FontSizeProperty),
		},
	});

	private static FuncTemplate<Control> FocusRing(double controlRadius) => new(() => new Border
	{
		BorderThickness = new Thickness(2.5),
		CornerRadius = new CornerRadius(controlRadius + 2.5),
		Margin = new Thickness(-2.5),
		IsHitTestVisible = false,
	}.Brush(Border.BorderBrushProperty, Keys.FocusRing));

	private static Style On(Func<Selector?, Selector> selector, params (AvaloniaProperty Property, object? Value)[] setters)
	{
		var style = new Style(selector);
		foreach (var (property, value) in setters)
		{
			style.Setters.Add(new Setter(property, value));
		}

		return style;
	}

	private static Selector Presenter(Selector? s, Func<Selector?, Selector> owner, string? pseudo = null)
	{
		var target = owner(s);
		if (pseudo is not null)
		{
			target = target.Class(pseudo);
		}

		return target.Template().OfType<ContentPresenter>().Name("PART_ContentPresenter");
	}

	private static Selector TextBoxBorder(Selector? s, string? cls, string? pseudo)
	{
		var target = s.OfType<TextBox>();
		if (cls is not null)
		{
			target = target.Class(cls);
		}

		if (pseudo is not null)
		{
			target = target.Class(pseudo);
		}

		return target.Template().OfType<Border>().Name("PART_BorderElement");
	}

	private static IEnumerable<Style> ControlStyles()
	{
		var transparent = Avalonia.Media.Brushes.Transparent;
		yield return On(s => s.OfType<TextBlock>(), (TextBlock.FontSizeProperty, BaseFontSize));

		// Buttons: soft filled chips with no outline, like macOS toolbar and Raycast action buttons.
		yield return On(s => s.OfType<Button>(),
			(TemplatedControl.CornerRadiusProperty, new CornerRadius(6)),
			(TemplatedControl.FontSizeProperty, 12.0),
			(TemplatedControl.FontWeightProperty, FontWeight.Medium),
			(TemplatedControl.PaddingProperty, new Thickness(10, 4)),
			(Layoutable.MinHeightProperty, 26.0),
			(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
			(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center),
			(TemplatedControl.BackgroundProperty, Brush(Keys.Control)),
			(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
			(TemplatedControl.ForegroundProperty, Brush(Keys.LabelPrimary)));
		yield return On(s => Presenter(s, x => x.OfType<Button>(), ":pointerover"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.ControlHover)),
			(ContentPresenter.ForegroundProperty, Brush(Keys.LabelPrimary)));
		yield return On(s => Presenter(s, x => x.OfType<Button>(), ":pressed"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Selection)),
			(ContentPresenter.ForegroundProperty, Brush(Keys.LabelPrimary)));

		// Keyboard focus: a soft accent ring hugging the control's own corners, like macOS, instead of
		// Fluent's rectangle. Fluent still shows it only for keyboard focus.
		yield return On(s => s.OfType<Button>(), (Control.FocusAdornerProperty, FocusRing(6)));
		// Fluent fills a focused dropdown with the accent, offset from the rounded chip. The chip draws the ring on
		// its own edge instead, because an adorner does not line up with Fluent's dropdown template.
		yield return On(s => s.OfType<ComboBox>(), (Control.FocusAdornerProperty, null));
		yield return On(s => s.OfType<ComboBox>().Template().OfType<Border>().Name("HighlightBackground"), (Visual.OpacityProperty, 0.0));
		yield return On(s => s.OfType<ComboBox>().Class(":focus-visible").Template().OfType<Border>().Name("Background"),
			(Border.BorderBrushProperty, Brush(Keys.FocusRing)),
			(Border.BorderThicknessProperty, new Thickness(2)));
		yield return On(s => s.OfType<AppleSwitch>(), (Control.FocusAdornerProperty, FocusRing(11)));
		yield return On(s => s.OfType<TextBox>(), (Control.FocusAdornerProperty, null));
		yield return On(s => s.OfType<Calendar>(), (Control.FocusAdornerProperty, null));

		yield return On(s => s.OfType<Button>().Class("primary"),
			(TemplatedControl.BackgroundProperty, Brush(Keys.Accent)),
			(TemplatedControl.ForegroundProperty, Brush(Keys.OnAccent)));
		yield return On(s => Presenter(s, x => x.OfType<Button>().Class("primary"), ":pointerover"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Accent)),
			(ContentPresenter.ForegroundProperty, Brush(Keys.OnAccent)),
			(Visual.OpacityProperty, 0.9));
		yield return On(s => Presenter(s, x => x.OfType<Button>().Class("primary"), ":pressed"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Accent)),
			(ContentPresenter.ForegroundProperty, Brush(Keys.OnAccent)),
			(Visual.OpacityProperty, 0.8));

		yield return On(s => s.OfType<Button>().Class("plain"),
			(TemplatedControl.BackgroundProperty, transparent),
			(TemplatedControl.ForegroundProperty, Brush(Keys.LabelSecondary)));
		yield return On(s => Presenter(s, x => x.OfType<Button>().Class("plain"), ":pointerover"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Control)),
			(ContentPresenter.ForegroundProperty, Brush(Keys.LabelPrimary)));

		yield return On(s => s.OfType<Button>().Class("icon"),
			(TemplatedControl.PaddingProperty, new Thickness(0)),
			(Layoutable.WidthProperty, 28.0),
			(Layoutable.HeightProperty, 28.0),
			(Layoutable.MinHeightProperty, 0.0),
			(ContentControl.HorizontalContentAlignmentProperty, HorizontalAlignment.Center),
			(ContentControl.VerticalContentAlignmentProperty, VerticalAlignment.Center));

		// Text fields: a soft fill and an accent ring on focus, like macOS search fields.
		yield return On(s => s.OfType<TextBox>(),
			(TemplatedControl.CornerRadiusProperty, ControlRadius),
			(TemplatedControl.FontSizeProperty, BaseFontSize),
			(TemplatedControl.PaddingProperty, new Thickness(9, 6)),
			(Layoutable.MinHeightProperty, 30.0),
			(TemplatedControl.BackgroundProperty, Brush(Keys.Control)),
			(TemplatedControl.BorderBrushProperty, transparent),
			(TemplatedControl.BorderThicknessProperty, new Thickness(1)),
			(TemplatedControl.ForegroundProperty, Brush(Keys.LabelPrimary)),
			(TextBox.CaretBrushProperty, Brush(Keys.Accent)));
		yield return On(s => TextBoxBorder(s, null, ":pointerover"),
			(Border.BackgroundProperty, Brush(Keys.Control)),
			(Border.BorderBrushProperty, transparent));
		yield return On(s => TextBoxBorder(s, null, ":focus"),
			(Border.BackgroundProperty, Brush(Keys.Control)),
			(Border.BorderBrushProperty, Brush(Keys.Accent)),
			(Border.BorderThicknessProperty, new Thickness(1)));

		// Bare fields carry no chrome at all; the big launcher-style inputs use them.
		yield return On(s => s.OfType<TextBox>().Class("bare"),
			(TemplatedControl.BackgroundProperty, transparent),
			(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
			(TemplatedControl.PaddingProperty, new Thickness(0)));
		foreach (var pseudo in new string?[] { null, ":pointerover", ":focus" })
		{
			yield return On(s => TextBoxBorder(s, "bare", pseudo),
				(Border.BackgroundProperty, transparent),
				(Border.BorderBrushProperty, transparent),
				(Border.BorderThicknessProperty, new Thickness(0)));
		}

		yield return On(s => s.OfType<ComboBox>(),
			(TemplatedControl.CornerRadiusProperty, new CornerRadius(6)),
			(TemplatedControl.FontSizeProperty, BaseFontSize),
			(Layoutable.MinHeightProperty, 26.0),
			(TemplatedControl.PaddingProperty, new Thickness(10, 3)),
			(TemplatedControl.BackgroundProperty, Brush(Keys.Control)),
			(TemplatedControl.BorderThicknessProperty, new Thickness(0)),
			(TemplatedControl.ForegroundProperty, Brush(Keys.LabelPrimary)));

		// A compact chip for the date choice next to the big inputs.
		yield return On(s => s.OfType<ComboBox>().Class("chip"),
			(TemplatedControl.FontSizeProperty, 12.0),
			(TemplatedControl.PaddingProperty, new Thickness(9, 2)),
			(Layoutable.MinHeightProperty, 24.0),
			(TemplatedControl.ForegroundProperty, Brush(Keys.LabelSecondary)));

		yield return On(s => s.OfType<ComboBoxItem>(),
			(TemplatedControl.CornerRadiusProperty, new CornerRadius(5)),
			(TemplatedControl.PaddingProperty, new Thickness(10, 4)),
			(Layoutable.MarginProperty, new Thickness(4, 1)),
			(Layoutable.MinHeightProperty, 0.0),
			(TemplatedControl.FontSizeProperty, BaseFontSize));

		// Keyboard navigation highlights the entry like a macOS menu instead of drawing a focus rectangle.
		yield return On(s => s.OfType<ComboBoxItem>(), (Control.FocusAdornerProperty, null));
		yield return On(s => Presenter(s, x => x.OfType<ComboBoxItem>(), ":focus-visible"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Accent)),
			(ContentPresenter.ForegroundProperty, Brush(Keys.OnAccent)));

		yield return On(s => s.OfType<Calendar>(),
			(TemplatedControl.FontSizeProperty, 12.0),
			(Layoutable.MinWidthProperty, 0.0),
			(Layoutable.HorizontalAlignmentProperty, HorizontalAlignment.Left),
			(TemplatedControl.BackgroundProperty, transparent),
			(TemplatedControl.BorderThicknessProperty, new Thickness(0)));
		// Day cells drawn as circles like the macOS calendar: today in the accent colour, the chosen day filled.
		yield return On(s => s.OfType<CalendarDayButton>(),
			(TemplatedControl.TemplateProperty, DayTemplate()),
			(TemplatedControl.FontSizeProperty, 12.0),
			(TemplatedControl.BackgroundProperty, transparent),
			(TemplatedControl.ForegroundProperty, Brush(Keys.LabelPrimary)),
			(Layoutable.WidthProperty, 30.0),
			(Layoutable.HeightProperty, 30.0),
			(Layoutable.MinWidthProperty, 0.0),
			(Layoutable.MinHeightProperty, 0.0),
			(Layoutable.MarginProperty, new Thickness(2)),
			(Control.FocusAdornerProperty, null));
		yield return On(s => s.OfType<CalendarDayButton>().Class(":inactive"), (TemplatedControl.ForegroundProperty, Brush(Keys.LabelTertiary)));
		yield return On(s => s.OfType<CalendarDayButton>().Class(":pointerover"), (TemplatedControl.BackgroundProperty, Brush(Keys.Control)));
		yield return On(s => s.OfType<CalendarDayButton>().Class(":today"),
			(TemplatedControl.ForegroundProperty, Brush(Keys.Accent)),
			(TemplatedControl.FontWeightProperty, FontWeight.Bold));
		yield return On(s => s.OfType<CalendarDayButton>().Class(":selected"),
			(TemplatedControl.BackgroundProperty, Brush(Keys.Accent)),
			(TemplatedControl.ForegroundProperty, Brush(Keys.OnAccent)));
		yield return On(s => s.OfType<CalendarItem>(),
			(Layoutable.MinWidthProperty, 0.0),
			(Layoutable.MinHeightProperty, 0.0),
			(Layoutable.WidthProperty, double.NaN),
			(Layoutable.HeightProperty, double.NaN));
		foreach (var part in new[] { "PART_PreviousButton", "PART_NextButton" })
		{
			yield return On(s => s.OfType<CalendarItem>().Template().OfType<Button>().Name(part).Descendant().OfType<Avalonia.Controls.Shapes.Path>(),
				(Layoutable.WidthProperty, 11.0),
				(Layoutable.HeightProperty, 6.0),
				(Avalonia.Controls.Shapes.Shape.StretchProperty, Stretch.Uniform));
		}

		yield return On(s => s.OfType<CalendarItem>().Template().OfType<Button>().Name("PART_PreviousButton"),
			(Layoutable.WidthProperty, 28.0), (Layoutable.HeightProperty, 28.0), (TemplatedControl.PaddingProperty, new Thickness(8)));
		yield return On(s => s.OfType<CalendarItem>().Template().OfType<Button>().Name("PART_NextButton"),
			(Layoutable.WidthProperty, 28.0), (Layoutable.HeightProperty, 28.0), (TemplatedControl.PaddingProperty, new Thickness(8)));
		yield return On(s => s.OfType<CalendarItem>().Template().OfType<Button>(),
			(TemplatedControl.BackgroundProperty, transparent),
			(TemplatedControl.FontSizeProperty, 13.0),
			(TemplatedControl.FontWeightProperty, FontWeight.SemiBold),
			(Layoutable.MinHeightProperty, 0.0));
		yield return On(s => s.OfType<CalendarItem>().Template().OfType<Button>().Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Control)));
		yield return On(s => s.OfType<CalendarButton>(),
			(TemplatedControl.CornerRadiusProperty, new CornerRadius(8)),
			(TemplatedControl.FontSizeProperty, 12.0));

		yield return On(s => s.OfType<NumericUpDown>(),
			(TemplatedControl.CornerRadiusProperty, new CornerRadius(6)),
			(TemplatedControl.BackgroundProperty, Brush(Keys.Control)),
			(TemplatedControl.BorderThicknessProperty, new Thickness(0)));

		// List rows: rounded, with a neutral highlight rather than a coloured bar.
		yield return On(s => s.OfType<ListBoxItem>(),
			(TemplatedControl.CornerRadiusProperty, RowRadius),
			(TemplatedControl.PaddingProperty, new Thickness(10, 7)),
			(Layoutable.MarginProperty, new Thickness(6, 0)),
			(Layoutable.MinHeightProperty, 0.0));
		yield return On(s => Presenter(s, x => x.OfType<ListBoxItem>(), ":pointerover"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Control)));
		yield return On(s => Presenter(s, x => x.OfType<ListBoxItem>(), ":selected"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Selection)));
		yield return On(s => s.OfType<ListBoxItem>().Class(":selected").Class(":pointerover").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Selection)));
		yield return On(s => s.OfType<ListBoxItem>().Class(":selected").Class(":focus").Template().OfType<ContentPresenter>().Name("PART_ContentPresenter"),
			(ContentPresenter.BackgroundProperty, Brush(Keys.Selection)));

		// The selection already shows where the keyboard is, so rows draw no extra focus rectangle.
		yield return On(s => s.OfType<ListBoxItem>(), (Control.FocusAdornerProperty, null));

		// Menus: the macOS look with an accent highlight on the hovered item.
		yield return On(s => s.OfType<ContextMenu>(),
			(TemplatedControl.CornerRadiusProperty, RowRadius),
			(TemplatedControl.BackgroundProperty, Brush(Keys.Surface)),
			(TemplatedControl.BorderBrushProperty, Brush(Keys.Separator)),
			(TemplatedControl.PaddingProperty, new Thickness(5)));
		yield return On(s => s.OfType<MenuItem>(),
			(TemplatedControl.CornerRadiusProperty, new CornerRadius(5)),
			(TemplatedControl.FontSizeProperty, BaseFontSize),
			(TemplatedControl.PaddingProperty, new Thickness(8, 4)),
			(Layoutable.MinHeightProperty, 0.0));
	}
}
