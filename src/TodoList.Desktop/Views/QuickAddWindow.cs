using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TodoList.Core.Input;
using TodoList.Desktop.Localization;
using TodoList.Desktop.Theme;
using TodoList.Desktop.ViewModels;

namespace TodoList.Desktop.Views;

/// <summary>
/// The launcher-style window the global shortcut opens. Everything works from the keyboard: type the
/// title, Tab to the due date dropdown and open it with Enter. Its last entry, Custom, opens a second row
/// to type any date in words or step through suggestions. Enter adds from the title or the custom date,
/// Escape or clicking elsewhere cancels.
/// </summary>
internal sealed class QuickAddWindow : Window
{
	public const double WindowWidth = 580;
	public const double WindowHeight = 100;
	private const double CustomRowHeight = 38;

	private const double IconColumn = 22;
	private const double Inset = 18;
	private const double TextStart = Inset + IconColumn + 12;

	private readonly TodoListViewModel _model;
	private readonly TextBox _input = new() { PlaceholderText = DesktopText.QuickAddPlaceholder, FontSize = 19, VerticalContentAlignment = VerticalAlignment.Center };
	private readonly ComboBox _dueChoice = new() { VerticalAlignment = VerticalAlignment.Center };
	private readonly DueField _due = new() { FontSize = 13, IsVisible = false };
	private readonly TextBlock _status = Ui.Text(DesktopText.AppName, Ui.Small, FontWeight.Medium, Keys.LabelSecondary);
	private int _hintFocus = -1;
	private readonly StackPanel _hints = new() { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };

	public QuickAddWindow(TodoListViewModel model)
	{
		_model = model;
		_input.Classes.Add("bare");
		_due.Classes.Add("bare");
		_dueChoice.Classes.Add("chip");
		_dueChoice.ItemsSource = DuePresets.All.Select(Ui.PresetLabel).Append(DesktopText.DueCustom).ToList();

		Title = DesktopText.TrayQuickAdd;
		Width = WindowWidth;
		Height = WindowHeight;
		CanResize = false;
		ShowInTaskbar = false;
		Topmost = true;
		WindowDecorations = WindowDecorations.None;
		// No window blur: the platform blurs the whole rectangle, which would show behind the rounded corners.
		TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
		Background = Brushes.Transparent;

		var inputRow = new Grid { ColumnDefinitions = new ColumnDefinitions($"{IconColumn},*,Auto"), ColumnSpacing = 12, Margin = new Thickness(Inset, 0, 12, 0), Height = 60 };
		inputRow.Children.Add(Icons.Create(Icons.Plus, IconColumn, Keys.Accent));
		Grid.SetColumn(_input, 1);
		inputRow.Children.Add(_input);
		Grid.SetColumn(_dueChoice, 2);
		inputRow.Children.Add(_dueChoice);

		// The calendar button sits under the plus icon so both fields start at the same x.
		_due.Height = CustomRowHeight - 8;
		_due.Margin = new Thickness(Inset - 1, 0, 14, 8);
		_due.PickButton.Margin = new Thickness(0, 0, TextStart - Inset + 1 - _due.PickButton.Width, 0);

		var appLabel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
		appLabel.Children.Add(Icons.Create(Icons.Check, 13, Keys.Accent));
		appLabel.Children.Add(_status);
		var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(14, 0, 8, 0), Height = 36 };
		bar.Children.Add(appLabel);
		Grid.SetColumn(_hints, 1);
		bar.Children.Add(_hints);

		var layout = new StackPanel { Children = { inputRow, _due, Ui.Divider(), bar } };
		KeyboardNavigation.SetTabNavigation(layout, KeyboardNavigationMode.Cycle);
		var frame = new Border
		{
			CornerRadius = AppTheme.LauncherRadius,
			BorderThickness = new Thickness(1),
			ClipToBounds = true,
			Child = layout,
		};
		frame.Brush(Border.BackgroundProperty, Keys.Window).Brush(Border.BorderBrushProperty, Keys.Separator);
		Content = frame;

		_input.GotFocus += (_, _) => ShowHints();
		_dueChoice.GotFocus += (_, _) => ShowHints();
		_due.GotFocus += (_, _) => ShowHints();
		_dueChoice.SelectionChanged += (_, _) => ShowCustomRow(IsCustom);
		// Picked from the open list, Custom takes the focus; stepping through the closed dropdown with the
		// arrow keys only reveals the row, so the arrows keep working there and Tab moves on.
		_dueChoice.DropDownClosed += (_, _) =>
		{
			if (IsCustom)
			{
				Dispatcher.UIThread.Post(() => _due.Focus(NavigationMethod.Tab));
			}
		};
		AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
		ShowHints();
	}

	public event EventHandler? Dismissed;

	public void Prepare()
	{
		_input.Text = string.Empty;
		_due.Text = string.Empty;
		_dueChoice.SelectedIndex = DuePresets.All.ToList().IndexOf(_model.Settings.DefaultDue);
		ShowStatus(DesktopText.AppName, Keys.LabelSecondary);
		Dispatcher.UIThread.Post(() => _input.Focus());
	}

	public void Dismiss()
	{
		Hide();
		Dismissed?.Invoke(this, EventArgs.Empty);
	}

	private bool IsCustom => _dueChoice.SelectedIndex == DuePresets.All.Count;

	// Custom grows the window by a row for the typed date and moves the focus there.
	private void ShowCustomRow(bool show)
	{
		if (_due.IsVisible == show)
		{
			return;
		}

		_due.IsVisible = show;
		Height = WindowHeight + (show ? CustomRowHeight : 0);
	}

	// The hints follow the focus: the title field points to the date, the date field to its suggestions.
	private void ShowHints()
	{
		// Focus moving between the entries of the open dropdown must not rebuild the bar each time.
		var focus = _due.IsFocused ? 2 : _dueChoice.IsKeyboardFocusWithin || _dueChoice.IsDropDownOpen ? 1 : 0;
		if (focus == _hintFocus && _hints.Children.Count > 0)
		{
			return;
		}

		_hintFocus = focus;
		_hints.Children.Clear();
		if (_due.IsFocused)
		{
			_hints.Children.Add(KeyHint.Create(DesktopText.HintSuggest, null, KeyHint.Up, KeyHint.Down));
		}
		else if (focus == 1)
		{
			// Enter belongs to the dropdown here, so it offers choosing instead of adding.
			_hints.Children.Add(KeyHint.Create(DesktopText.HintChoose, () => _dueChoice.IsDropDownOpen = true, KeyHint.Enter));
			_hints.Children.Add(KeyHint.Create(DesktopText.HintAdd, () => _ = AddAsync(), KeyHint.Command, KeyHint.Enter));
			_hints.Children.Add(KeyHint.Create(DesktopText.HintCancel, Dismiss, KeyHint.Escape));
			return;
		}
		else
		{
			_hints.Children.Add(KeyHint.Create(DesktopText.HintDueField, () => _dueChoice.Focus(), KeyHint.Tab));
		}

		_hints.Children.Add(KeyHint.Create(DesktopText.HintAdd, () => _ = AddAsync(), KeyHint.Enter));
		_hints.Children.Add(KeyHint.Create(DesktopText.HintCancel, Dismiss, KeyHint.Escape));
	}

	private void ShowStatus(string text, string brush)
	{
		_status.Text = text;
		_status.Brush(TextBlock.ForegroundProperty, brush);
	}

	private async Task AddAsync()
	{
		var title = _input.Text ?? string.Empty;
		if (title.Trim().Length == 0)
		{
			_input.Focus();
			return;
		}

		DateTimeOffset? dueAt;
		if (!IsCustom)
		{
			dueAt = DuePresets.Resolve(Ui.SelectedPreset(_dueChoice), DateTimeOffset.UtcNow, TimeZoneInfo.Local);
		}
		else if (!_due.TryGetDue(out dueAt))
		{
			ShowStatus(DesktopText.InvalidDue, Keys.Red);
			_due.Focus();
			return;
		}

		// Cleared before awaiting so a quick second Enter cannot add the same todo twice.
		_input.Text = string.Empty;
		if (await _model.CreateAsync(title, dueAt))
		{
			Dismiss();
			return;
		}

		_input.Text = title;
		if (_model.Message is { } message)
		{
			ShowStatus(message, Keys.Red);
		}
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		// An open dropdown uses Enter and Escape itself, and Enter on the closed one opens it; Command+Enter
		// still adds from there.
		var commandEnter = e.Key == Key.Enter && e.KeyModifiers == (KeyHint.IsMac ? KeyModifiers.Meta : KeyModifiers.Control);
		if (_dueChoice.IsDropDownOpen || (e.Key == Key.Enter && !commandEnter && _dueChoice.IsKeyboardFocusWithin))
		{
			return;
		}

		if (e.Key == Key.Escape)
		{
			e.Handled = true;
			Dismiss();
		}
		else if (e.Key == Key.Enter)
		{
			e.Handled = true;
			_ = AddAsync();
		}
	}
}
