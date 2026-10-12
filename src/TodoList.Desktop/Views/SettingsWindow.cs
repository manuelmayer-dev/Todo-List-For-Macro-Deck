using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Serilog;
using TodoList.Core;
using TodoList.Core.Hotkeys;
using TodoList.Core.Input;
using TodoList.Core.Settings;
using TodoList.Core.Storage;
using TodoList.Desktop.Hotkeys;
using TodoList.Desktop.Localization;
using TodoList.Desktop.Theme;

namespace TodoList.Desktop.Views;

/// <summary>
/// Desktop settings. Every change is saved immediately; the shortcut is re-registered and its status shown.
/// </summary>
internal sealed class SettingsWindow : Window
{
	private readonly SettingsService _settings;
	private readonly DataLocationService _location;
	private readonly TextBlock _locationPath = Ui.Text(string.Empty, 11, brush: Keys.LabelSecondary);
	private readonly TextBlock _locationStatus = Ui.Text(string.Empty, 11, brush: Keys.LabelSecondary);
	private readonly Button _locationDefault;
	private readonly StackPanel _locationChoice = new() { Spacing = 8, IsVisible = false, Margin = new Thickness(12, 0, 12, 10) };
	private string? _pendingDirectory;
	private bool _moving;
	private readonly HotkeyState _hotkey;
	private readonly Action<bool> _suspendHotkey;
	private readonly ILogger _logger;
	private readonly bool _isMac = OperatingSystem.IsMacOS();
	private readonly AppleSwitch _hotkeyEnabled = Check(DesktopText.HotkeyEnabled);
	private readonly TextBox _hotkeyBox = new() { IsReadOnly = true, Width = 150, HorizontalContentAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Cursor = new Cursor(StandardCursorType.Hand) };
	private readonly TextBlock _hotkeyStatus = Ui.Text(string.Empty, 11, brush: Keys.LabelSecondary);
	private readonly ComboBox _theme = Choice(DesktopText.ThemeSystem, DesktopText.ThemeLight, DesktopText.ThemeDark);
	private readonly ComboBox _sort = Choice(DesktopText.SortDueDate, DesktopText.SortCreated, DesktopText.SortManual);
	private readonly ComboBox _defaultDue = WithWidth(Ui.DuePresetBox(DuePreset.None));
	private readonly MinutesPicker _dueSoon = new();
	private readonly MinutesPicker _grace = new();
	private readonly AppleSwitch _showCompleted = Check(DesktopText.ShowCompletedSetting);
	private readonly AppleSwitch _confirmDelete = Check(DesktopText.ConfirmDeleteSetting);
	private readonly AppleSwitch _closeOnFocusLoss = Check(DesktopText.CloseOnFocusLoss);
	private readonly AppleSwitch _rememberState = Check(DesktopText.RememberState);
	private bool _loading;
	private bool _recording;

	public SettingsWindow(SettingsService settings, DataLocationService location, HotkeyState hotkey, Action<bool> suspendHotkey, ILogger logger)
	{
		_settings = settings;
		_location = location;
		_locationDefault = Ui.Button(DesktopText.StorageDefault, () => _ = MoveAsync(_location.DefaultDirectory, useExisting: null));
		_hotkey = hotkey;
		_suspendHotkey = suspendHotkey;
		_logger = logger.ForContext<SettingsWindow>();

		Title = DesktopText.SettingsTitle;
		Width = 540;
		SizeToContent = SizeToContent.Height;
		CanResize = false;
		WindowStartupLocation = WindowStartupLocation.CenterScreen;
		this.Brush(BackgroundProperty, Keys.Window);
		_hotkeyStatus.Wrapping();

		var content = new StackPanel { Spacing = 18, Margin = new Thickness(20, 18, 20, 22) };
		content.Children.Add(Group(
			DesktopText.SectionShortcut,
			DesktopText.SectionShortcutHint,
			Row(DesktopText.HotkeyEnabled, null, _hotkeyEnabled),
			Row(DesktopText.HotkeyRecord, _hotkeyStatus, _hotkeyBox)));
		content.Children.Add(Group(
			DesktopText.SectionAppearance,
			null,
			Row(DesktopText.Theme, null, _theme)));
		content.Children.Add(Group(
			DesktopText.SectionTodos,
			DesktopText.SectionDueHint,
			Row(DesktopText.DefaultSort, null, _sort),
			Row(DesktopText.DefaultDue, null, _defaultDue),
			Row(DesktopText.DueSoonLabel, null, _dueSoon),
			Row(DesktopText.OverdueLabel, null, _grace),
			Row(DesktopText.ShowCompletedSetting, null, _showCompleted),
			Row(DesktopText.ConfirmDeleteSetting, null, _confirmDelete)));
		content.Children.Add(Group(
			DesktopText.SectionPopup,
			DesktopText.SectionPopupHint,
			Row(DesktopText.CloseOnFocusLoss, null, _closeOnFocusLoss),
			Row(DesktopText.RememberState, null, _rememberState)));
		content.Children.Add(Group(
			DesktopText.SectionStorage,
			DesktopText.SectionStorageHint,
			StorageRows()));
		Content = new ScrollViewer { Content = content, MaxHeight = 780 };

		Load(settings.Current);
		foreach (var box in new[] { _hotkeyEnabled, _showCompleted, _confirmDelete, _closeOnFocusLoss, _rememberState })
		{
			box.IsCheckedChanged += (_, _) => Save();
		}

		foreach (var combo in new[] { _theme, _sort, _defaultDue })
		{
			combo.SelectionChanged += (_, _) => Save();
		}

		_dueSoon.SelectionChanged += (_, _) => Save();
		_grace.SelectionChanged += (_, _) => Save();
		_hotkeyBox.GotFocus += (_, _) => StartRecording();
		_hotkeyBox.LostFocus += (_, _) => StopRecording();
		_hotkeyBox.AddHandler(KeyDownEvent, OnRecordKey, RoutingStrategies.Tunnel);
		_hotkey.Changed += OnHotkeyChanged;
		_settings.Changed += OnSettingsChanged;
		ShowStatus();
		ShowLocation();
	}

	protected override void OnClosed(EventArgs e)
	{
		_hotkey.Changed -= OnHotkeyChanged;
		_settings.Changed -= OnSettingsChanged;
		base.OnClosed(e);
	}

	private static AppleSwitch Check(string text) => new() { Tag = text };

	private static ComboBox WithWidth(ComboBox box)
	{
		box.MinWidth = ChoiceWidth;
		return box;
	}

	private static ComboBox Choice(params string[] items) => new() { ItemsSource = items, MinWidth = ChoiceWidth };

	private const double ChoiceWidth = 170;

	/// <summary>A group like the macOS System Settings: a heading, a rounded box of rows, an optional footnote.</summary>
	private static StackPanel Group(string title, string? footnote, params Control[] rows)
	{
		var box = new StackPanel();
		for (var i = 0; i < rows.Length; i++)
		{
			if (i > 0)
			{
				box.Children.Add(new Border { Height = 1, Margin = new Thickness(12, 0, 0, 0) }.Brush(Border.BackgroundProperty, Keys.Separator));
			}

			box.Children.Add(rows[i]);
		}

		var group = new StackPanel { Spacing = 6 };
		var heading = Ui.Text(title, Ui.Body, FontWeight.SemiBold);
		heading.Margin = new Thickness(4, 0, 0, 0);
		group.Children.Add(heading);
		group.Children.Add(new Border
		{
			CornerRadius = new CornerRadius(10),
			BorderThickness = new Thickness(1),
			Child = box,
		}.Brush(Border.BackgroundProperty, Keys.Surface).Brush(Border.BorderBrushProperty, Keys.Separator));
		if (footnote is not null)
		{
			var note = Ui.Text(footnote, 11, brush: Keys.LabelSecondary).Wrapping();
			note.Margin = new Thickness(4, 0, 4, 0);
			group.Children.Add(note);
		}

		return group;
	}

	private static Grid Row(string label, Control? below, Control control)
	{
		var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 16, Margin = new Thickness(12, 8), MinHeight = 28 };
		var left = new StackPanel { Spacing = 2, VerticalAlignment = VerticalAlignment.Center, Children = { Ui.Text(label).Wrapping() } };
		if (below is not null)
		{
			left.Children.Add(below);
		}

		grid.Children.Add(left);
		control.VerticalAlignment = VerticalAlignment.Center;
		control.HorizontalAlignment = HorizontalAlignment.Right;
		Grid.SetColumn(control, 1);
		grid.Children.Add(control);
		return grid;
	}

	// Moving to a folder with other todos also brings their settings along.
	private void OnSettingsChanged(object? sender, TodoSettings settings) => Dispatcher.UIThread.Post(() =>
	{
		if (!_recording)
		{
			Load(_settings.Current);
		}
	});

	private StackPanel StorageRows()
	{
		_locationPath.TextTrimming = TextTrimming.PrefixCharacterEllipsis;
		var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
		buttons.Children.Add(Ui.Button(DesktopText.StorageReveal, Reveal));
		buttons.Children.Add(_locationDefault);
		buttons.Children.Add(Ui.Button(DesktopText.StorageChoose, () => _ = ChooseAsync()));

		var question = Ui.Text(DesktopText.StorageExisting, 12);
		var answers = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right };
		answers.Children.Add(Ui.Button(DesktopText.HintCancel, () => ShowChoice(null)));
		answers.Children.Add(Ui.Button(DesktopText.StorageReplace, () => _ = MoveAsync(_pendingDirectory!, useExisting: false)));
		answers.Children.Add(Ui.Button(DesktopText.StorageUseExisting, () => _ = MoveAsync(_pendingDirectory!, useExisting: true), "primary"));
		_locationChoice.Children.Add(question);
		_locationChoice.Children.Add(answers);

		var details = new StackPanel { Spacing = 2 };
		details.Children.Add(_locationPath);
		details.Children.Add(_locationStatus.Wrapping());
		var panel = new StackPanel();
		panel.Children.Add(Row(DesktopText.StorageLocation, details, buttons));
		panel.Children.Add(_locationChoice);
		return panel;
	}

	private void ShowLocation()
	{
		_locationPath.Text = _location.CurrentDirectory;
		ToolTip.SetTip(_locationPath, _location.CurrentDirectory);
		_locationDefault.IsVisible = !_location.IsDefault;
	}

	private void ShowChoice(string? directory)
	{
		_pendingDirectory = directory;
		_locationChoice.IsVisible = directory is not null;
	}

	private async Task ChooseAsync()
	{
		var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
		{
			Title = DesktopText.StoragePickerTitle,
			AllowMultiple = false,
			SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(_location.CurrentDirectory),
		});
		if (folders.Count == 1 && folders[0].TryGetLocalPath() is { } path)
		{
			await MoveAsync(path, useExisting: null);
		}
	}

	/// <summary>Moves the data; with other todos in the target and no answer yet, asks first.</summary>
	private async Task MoveAsync(string directory, bool? useExisting)
	{
		if (_moving)
		{
			return;
		}

		if (useExisting is null && _location.HasOtherData(directory))
		{
			_locationStatus.Text = string.Empty;
			ShowChoice(directory);
			return;
		}

		ShowChoice(null);
		_moving = true;
		try
		{
			var result = await Task.Run(() => _location.ChangeAsync(directory, useExisting == true, CancellationToken.None));
			_locationStatus.Text = result switch
			{
				DataLocationResult.Moved => DesktopText.StorageMoved,
				DataLocationResult.UsedExisting => DesktopText.StorageUsedExisting,
				_ => string.Empty,
			};
			_locationStatus.Brush(TextBlock.ForegroundProperty, Keys.LabelSecondary);
		}
		catch (TodoStoreException exception)
		{
			_logger.Error(exception, "Moving the todo data to {Directory} failed", directory);
			_locationStatus.Text = DesktopText.StorageFailed;
			_locationStatus.Brush(TextBlock.ForegroundProperty, Keys.Red);
		}
		finally
		{
			_moving = false;
			ShowLocation();
		}
	}

	private void Reveal()
	{
		try
		{
			_ = Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(_location.CurrentDirectory));
		}
		catch (Exception exception)
		{
			_logger.Warning(exception, "Could not show the data folder");
		}
	}

	private void Load(TodoSettings settings)
	{
		_loading = true;
		_hotkeyEnabled.IsChecked = settings.HotkeyEnabled;
		_hotkeyBox.Text = HotkeyGesture.TryParse(settings.Hotkey, out var gesture) ? gesture.ToDisplayString(_isMac) : settings.Hotkey;
		_theme.SelectedIndex = (int)settings.Theme;
		_sort.SelectedIndex = (int)settings.DefaultSort;
		_defaultDue.SelectedIndex = DuePresets.All.ToList().IndexOf(settings.DefaultDue);
		_dueSoon.Minutes = settings.DueSoonMinutes;
		_grace.Minutes = settings.OverdueGraceMinutes;
		_showCompleted.IsChecked = settings.ShowCompleted;
		_confirmDelete.IsChecked = settings.ConfirmDelete;
		_closeOnFocusLoss.IsChecked = settings.ClosePopupOnFocusLoss;
		_rememberState.IsChecked = settings.RememberPopupState;
		_loading = false;
	}

	private void Save(string? hotkey = null)
	{
		if (_loading)
		{
			return;
		}

		var updated = _settings.Current with
		{
			HotkeyEnabled = _hotkeyEnabled.IsChecked == true,
			Hotkey = hotkey ?? _settings.Current.Hotkey,
			Theme = (ThemePreference)Math.Max(0, _theme.SelectedIndex),
			DefaultSort = (TodoSortOrder)Math.Max(0, _sort.SelectedIndex),
			DefaultDue = Ui.SelectedPreset(_defaultDue),
			DueSoonMinutes = _dueSoon.Minutes,
			OverdueGraceMinutes = _grace.Minutes,
			ShowCompleted = _showCompleted.IsChecked == true,
			ConfirmDelete = _confirmDelete.IsChecked == true,
			ClosePopupOnFocusLoss = _closeOnFocusLoss.IsChecked == true,
			RememberPopupState = _rememberState.IsChecked == true,
		};
		_ = SaveAsync(updated);
	}

	private async Task SaveAsync(TodoSettings settings)
	{
		try
		{
			await _settings.SaveAsync(settings, CancellationToken.None);
		}
		catch (TodoStoreException exception)
		{
			_logger.Error(exception, "Saving settings failed");
			_hotkeyStatus.Text = DesktopText.StoreFailed;
		}
	}

	private void StartRecording()
	{
		_recording = true;
		_suspendHotkey(true);
		_hotkeyBox.Text = DesktopText.HotkeyRecording;
	}

	private void StopRecording()
	{
		_recording = false;
		_suspendHotkey(false);
		Load(_settings.Current);
	}

	private void OnRecordKey(object? sender, KeyEventArgs e)
	{
		if (!_recording)
		{
			return;
		}

		e.Handled = true;
		if (e.Key == Key.Escape)
		{
			Focus();
			return;
		}

		if (!TryMapKey(e.Key, out var key))
		{
			return;
		}

		var gesture = new HotkeyGesture(Modifiers(e.KeyModifiers), key);
		var error = gesture.Validate(_isMac);
		if (error != HotkeyValidationError.None)
		{
			_hotkeyStatus.Text = error == HotkeyValidationError.OptionOnlyOnMac ? DesktopText.HotkeyOptionOnly : DesktopText.HotkeyInvalid;
			return;
		}

		_hotkeyBox.Text = gesture.ToDisplayString(_isMac);
		Save(gesture.ToString());
		Focus();
	}

	private static HotkeyModifiers Modifiers(KeyModifiers modifiers) =>
		(modifiers.HasFlag(KeyModifiers.Control) ? HotkeyModifiers.Control : HotkeyModifiers.None) |
		(modifiers.HasFlag(KeyModifiers.Alt) ? HotkeyModifiers.Alt : HotkeyModifiers.None) |
		(modifiers.HasFlag(KeyModifiers.Shift) ? HotkeyModifiers.Shift : HotkeyModifiers.None) |
		(modifiers.HasFlag(KeyModifiers.Meta) ? HotkeyModifiers.Meta : HotkeyModifiers.None);

	internal static bool TryMapKey(Key key, out string token)
	{
		token = key switch
		{
			>= Key.A and <= Key.Z => key.ToString(),
			>= Key.D0 and <= Key.D9 => ((int)(key - Key.D0)).ToString(System.Globalization.CultureInfo.InvariantCulture),
			>= Key.F1 and <= Key.F24 => key.ToString(),
			Key.Space => "Space",
			_ => string.Empty,
		};
		return token.Length > 0;
	}

	private void OnHotkeyChanged(object? sender, EventArgs e) => ShowStatus();

	private void ShowStatus()
	{
		_hotkeyStatus.Text = _hotkey.Result switch
		{
			null => DesktopText.HotkeyDisabled,
			HotkeyRegistrationResult.Registered => DesktopText.HotkeyRegistered,
			HotkeyRegistrationResult.InUse => DesktopText.HotkeyInUse,
			HotkeyRegistrationResult.Invalid => DesktopText.HotkeyInvalid,
			HotkeyRegistrationResult.Unsupported => DesktopText.HotkeyUnsupported,
			_ => DesktopText.HotkeyFailed,
		};
		_hotkeyStatus.Brush(
			TextBlock.ForegroundProperty,
			_hotkey.Result is null or HotkeyRegistrationResult.Registered ? Keys.LabelSecondary : Keys.Red);
	}
}
