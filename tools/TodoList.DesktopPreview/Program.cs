using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Serilog;
using TodoList.Core;
using TodoList.Core.Settings;
using TodoList.Core.Storage;
using TodoList.Desktop;
using TodoList.Desktop.ViewModels;
using TodoList.Desktop.Views;

namespace TodoList.DesktopPreview;

/// <summary>
/// Renders the popup, the popup's editor, quick add and the settings window in dark and light to PNG.
/// Usage: dotnet run --project tools/TodoList.DesktopPreview -- &lt;output directory&gt;
/// </summary>
internal static class Program
{
	[STAThread]
	public static int Main(string[] args)
	{
		var output = Path.GetFullPath(args.Length > 0 ? args[0] : "desktop-previews");

		// An optional second argument renders in another language, for example "de-DE".
		if (args.Length > 1)
		{
			var culture = System.Globalization.CultureInfo.GetCultureInfo(args[1]);
			System.Globalization.CultureInfo.DefaultThreadCurrentCulture = culture;
			System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = culture;
			System.Globalization.CultureInfo.CurrentCulture = culture;
			System.Globalization.CultureInfo.CurrentUICulture = culture;
		}
		Directory.CreateDirectory(output);
		AppBuilder.Configure(() => new PreviewApp(output))
			.UsePlatformDetect()
			.With(new MacOSPlatformOptions { ShowInDock = false })
			.StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
		return 0;
	}
}

internal sealed class PreviewApp(string output) : Application
{
	// PREVIEW_SCALE renders sharper images, for example 3 for store screenshots.
	private static readonly double Scale = double.TryParse(Environment.GetEnvironmentVariable("PREVIEW_SCALE"), System.Globalization.CultureInfo.InvariantCulture, out var scale) ? scale : 2;

	public override void Initialize() => TodoList.Desktop.Theme.AppTheme.Apply(this);

	public override void OnFrameworkInitializationCompleted()
	{
		base.OnFrameworkInitializationCompleted();
		Dispatcher.UIThread.Post(async () =>
		{
			try
			{
				await RenderAllAsync();
			}
			catch (Exception exception)
			{
				Console.Error.WriteLine(exception);
			}
			finally
			{
				((IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!).Shutdown();
			}
		});
	}

	private async Task RenderAllAsync()
	{
		var logger = new LoggerConfiguration().CreateLogger();
		var directory = Path.Combine(Path.GetTempPath(), "todolist-preview", Guid.NewGuid().ToString("N"));
		using var database = new TodoDatabase(Path.Combine(directory, "todos.db"), logger);
		using var todos = new TodoService(database, TimeProvider.System, logger);
		using var settings = new SettingsService(database, logger);
		var location = new DataLocationService(new StorageLocation(directory, logger), database, todos, settings, logger);
		await todos.LoadAsync(CancellationToken.None);
		await settings.LoadAsync(CancellationToken.None);
		await SeedAsync(todos);

		using var model = new TodoListViewModel(todos, settings, TimeProvider.System, logger, action => Dispatcher.UIThread.Post(action));
		model.Refresh();
		model.SelectedId = model.Rows[1].Id;

		foreach (var (variant, name) in new[] { (ThemeVariant.Dark, "dark"), (ThemeVariant.Light, "light") })
		{
			RequestedThemeVariant = variant;

			var popup = new PopupWindow(model, () => { });
			await RenderAsync(popup, $"popup-{name}");
			model.BeginEdit(model.Rows[1].Id);
			await RenderAsync(popup, $"popup-edit-{name}");
			model.CancelPending();
			popup.Close();

			var quickAdd = new QuickAddWindow(model);
			quickAdd.Prepare();
			await RenderAsync(quickAdd, $"quickadd-{name}");
			quickAdd.GetVisualDescendants().OfType<TextBox>().First().Text = "Pick up the parcel";
			await RenderAsync(quickAdd, $"quickadd-preset-{name}");
			quickAdd.GetVisualDescendants().OfType<ComboBox>().First().Focus(Avalonia.Input.NavigationMethod.Tab);
			await RenderAsync(quickAdd, $"quickadd-choice-focus-{name}");

			// Custom picked from the dropdown, then a typed due date in the row it opens.
			quickAdd.GetVisualDescendants().OfType<TextBox>().First().Text = "Pick up the parcel";
			var choice = quickAdd.GetVisualDescendants().OfType<ComboBox>().First();
			choice.SelectedIndex = choice.ItemCount - 1;
			var due = quickAdd.GetVisualDescendants().OfType<DueField>().Single();
			due.Text = "fri 18:00";
			due.Focus(Avalonia.Input.NavigationMethod.Tab);
			due.SelectionStart = due.SelectionEnd = due.CaretIndex = due.Text.Length;
			await RenderAsync(quickAdd, $"quickadd-typed-{name}");
			due.Text = "someday";
			await RenderAsync(quickAdd, $"quickadd-invalid-{name}");
			quickAdd.Close();

			var calendarHost = new Window
			{
				SizeToContent = SizeToContent.WidthAndHeight,
				Content = new Border { Padding = new Thickness(10), Child = DueField.CreateCalendar() },
			};
			// The Friday that "fri 18:00" in the quick-add render means.
			((Calendar)((Border)calendarHost.Content!).Child!).SelectedDate = DateTime.Today.AddDays(((int)DayOfWeek.Friday - (int)DateTime.Today.DayOfWeek + 7) % 7);
			await RenderAsync(calendarHost, $"calendar-{name}");
			calendarHost.Close();

			await RenderDropDownAsync($"dropdown-{name}");

			// Shown as registered, like on a machine where the shortcut works.
			var hotkeyState = new TodoList.Desktop.Hotkeys.HotkeyState();
			hotkeyState.Set(TodoList.Desktop.Hotkeys.HotkeyRegistrationResult.Registered);
			var settingsWindow = new SettingsWindow(settings, location, hotkeyState, _ => { }, logger);
			await RenderAsync(settingsWindow, $"settings-{name}");
			settingsWindow.GetVisualDescendants().OfType<Button>().First(b => b.Focusable && b.IsEffectivelyVisible).Focus(Avalonia.Input.NavigationMethod.Tab);
			await RenderAsync(settingsWindow, $"settings-focus-{name}");
			((ScrollViewer)settingsWindow.Content!).ScrollToEnd();
			await RenderAsync(settingsWindow, $"settings-end-{name}");

			// The whole window without its scroll limit, for store screenshots.
			((ScrollViewer)settingsWindow.Content!).MaxHeight = double.PositiveInfinity;
			((ScrollViewer)settingsWindow.Content!).ScrollToHome();
			// A sample folder instead of this run's temporary directory, like the sample todos.
			var path = settingsWindow.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == location.CurrentDirectory);
			path.Text = OperatingSystem.IsMacOS() ? "~/Library/Mobile Documents/com~apple~CloudDocs/Todo List" : @"C:\Users\you\OneDrive\Todo List";
			await Task.Delay(200);
			await RenderAsync(settingsWindow, $"settings-full-{name}");
			var trayHeading = settingsWindow.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == TodoList.Desktop.Localization.DesktopText.SectionPopup);
			Console.WriteLine($"settings-full split at {trayHeading.TranslatePoint(default, settingsWindow)!.Value.Y}");
			settingsWindow.Close();
		}

		Console.WriteLine($"Rendered previews to {output}");
	}

	private static async Task SeedAsync(TodoService todos)
	{
		var now = DateTimeOffset.UtcNow;
		// Round local times read like real due dates; only the "in 25 min" one is relative.
		var today = DateOnly.FromDateTime(DateTime.Now);
		DateTimeOffset At(int days, int hour) => new DateTimeOffset(today.AddDays(days).ToDateTime(new TimeOnly(hour, 0)), TimeZoneInfo.Local.GetUtcOffset(DateTime.Now));
		await todos.CreateAsync(new TodoDraft("Send the quarterly report", DueAt: now.AddHours(-3)), CancellationToken.None);
		await todos.CreateAsync(new TodoDraft("Book dentist appointment", "Ask about the cleaning", now.AddMinutes(25)), CancellationToken.None);
		await todos.CreateAsync(new TodoDraft("Water the plants", DueAt: At(1, 18)), CancellationToken.None);
		await todos.CreateAsync(new TodoDraft("Renew the domain before it expires next month"), CancellationToken.None);
		await todos.CreateAsync(new TodoDraft("Call the landlord", DueAt: At(3, 10)), CancellationToken.None);
		var done = await todos.CreateAsync(new TodoDraft("Order printer ink"), CancellationToken.None);
		await todos.CompleteAsync(done.Id, CancellationToken.None);
	}

	// Dropdowns open in their own popup window, so the open popup is rendered on its own.
	private async Task RenderDropDownAsync(string name)
	{
		var combo = TodoList.Desktop.Views.Ui.DuePresetBox(TodoList.Core.Input.DuePreset.TomorrowMorning);
		var host = new Window { Width = 260, Height = 60, Content = new Border { Padding = new Thickness(10), Child = combo } };
		host.Opacity = 0;
		host.ShowActivated = false;
		host.Show();
		await Task.Delay(300);
		combo.IsDropDownOpen = true;
		await Task.Delay(400);

		// Keyboard focus on an entry, to show the highlight arrow-key navigation gets.
		(combo.ContainerFromIndex(1) as Control)?.Focus(Avalonia.Input.NavigationMethod.Directional);
		await Task.Delay(200);
		var popup = combo.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Popup>().First();
		if (popup.Child is Visual root)
		{
			var size = root.Bounds.Size;
			using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(size, Scale), new Vector(96 * Scale, 96 * Scale));
			bitmap.Render(root);
			bitmap.Save(Path.Combine(output, name + ".png"), PngBitmapEncoderOptions.Default);
		}

		combo.IsDropDownOpen = false;
		host.Close();
	}

	private async Task RenderAsync(Window window, string name)
	{
		// Shown fully transparent and without activation, so the platform lays it out and applies the theme
		// while nothing appears on screen and no focus is taken.
		window.Opacity = 0;
		window.ShowActivated = false;
		window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		if (!window.IsVisible)
		{
			window.Show();
		}

		await Task.Delay(500);
		var size = window.Bounds.Size;
		using var bitmap = new RenderTargetBitmap(PixelSize.FromSize(size, Scale), new Vector(96 * Scale, 96 * Scale));
		window.Opacity = 1;
		bitmap.Render(window);
		window.Opacity = 0;
		bitmap.Save(Path.Combine(output, name + ".png"), PngBitmapEncoderOptions.Default);
	}
}
