using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using TodoList.Desktop.Localization;
using TodoList.Desktop.Theme;
using TodoList.Desktop.ViewModels;

namespace TodoList.Desktop.Views;

/// <summary>
/// The popover that opens from the tray, laid out like a launcher: an input line, the list and an action
/// bar with keyboard hints. Keyboard: arrows move, Space toggles, Enter edits or adds, Delete or Backspace
/// deletes, Ctrl or Cmd+N focuses the input, Ctrl or Cmd+D shows completed todos, Ctrl or Cmd+comma opens
/// settings, Escape closes or cancels.
/// </summary>
internal sealed class PopupWindow : Window
{
	public const double PopupWidth = 400;
	public const double PopupHeight = 500;

	private readonly TodoListViewModel _model;
	private readonly Action _openSettings;
	private readonly TextBox _quickAdd = new() { PlaceholderText = DesktopText.QuickAddPlaceholder, FontSize = 15, VerticalContentAlignment = VerticalAlignment.Center };
	private readonly ComboBox _quickAddDue;
	private readonly ListBox _list = new() { Background = Brushes.Transparent, SelectionMode = SelectionMode.Single, Padding = new Thickness(0, 6) };
	private readonly StackPanel _empty = new() { Spacing = 6, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
	private readonly TextBlock _emptyTitle = Ui.Text(string.Empty, 14, FontWeight.SemiBold, Keys.LabelSecondary);
	private readonly TextBlock _emptyHint = Ui.Text(DesktopText.EmptyHint, Ui.Small, brush: Keys.LabelTertiary);
	private readonly TextBlock _status = Ui.Text(string.Empty, Ui.Small, FontWeight.Medium, Keys.LabelSecondary);
	private readonly StackPanel _hints = new() { Orientation = Orientation.Horizontal, Spacing = 2, HorizontalAlignment = HorizontalAlignment.Right };
	private readonly EditPanel _edit;
	private bool _updating;

	public PopupWindow(TodoListViewModel model, Action openSettings)
	{
		_model = model;
		_openSettings = openSettings;
		_quickAddDue = Ui.DuePresetBox(model.QuickAddDue);
		_edit = new EditPanel(model, () => FocusList());

		Title = DesktopText.AppName;
		Width = PopupWidth;
		Height = PopupHeight;
		CanResize = false;
		ShowInTaskbar = false;
		Topmost = true;
		WindowDecorations = WindowDecorations.None;
		// No window blur: the platform blurs the whole rectangle, which would show behind the rounded corners.
		TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
		Background = Brushes.Transparent;

		_quickAdd.Classes.Add("bare");
		_quickAddDue.Classes.Add("chip");
		_emptyTitle.HorizontalAlignment = HorizontalAlignment.Center;
		_emptyHint.HorizontalAlignment = HorizontalAlignment.Center;
		var emptyIcon = Icons.Create(Icons.Check, 26, Keys.LabelTertiary);
		emptyIcon.HorizontalAlignment = HorizontalAlignment.Center;
		emptyIcon.Margin = new Thickness(0, 0, 0, 4);
		_empty.Children.Add(emptyIcon);
		_empty.Children.Add(_emptyTitle);
		_empty.Children.Add(_emptyHint);

		// The list asks for a template with no item while it recycles containers.
		_list.ItemTemplate = new FuncDataTemplate<TodoRow?>((row, _) => row is null ? new Panel() : Row(row), supportsRecycling: false);
		_list.SelectionChanged += (_, _) =>
		{
			if (!_updating)
			{
				_model.SelectedId = (_list.SelectedItem as TodoRow)?.Id;
				RenderActionBar(editing: false);
			}
		};
		_list.DoubleTapped += (_, _) => EditSelected();
		_quickAddDue.SelectionChanged += (_, _) => _model.QuickAddDue = Ui.SelectedPreset(_quickAddDue);

		var inputBar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,Auto"), ColumnSpacing = 10, Margin = new Thickness(16, 0, 10, 0), Height = 52 };
		inputBar.Children.Add(Icons.Create(Icons.Plus, 18, Keys.LabelSecondary));
		Grid.SetColumn(_quickAdd, 1);
		inputBar.Children.Add(_quickAdd);
		Grid.SetColumn(_quickAddDue, 2);
		inputBar.Children.Add(_quickAddDue);
		var settings = Ui.IconButton(Icons.Settings, DesktopText.Settings, _openSettings);
		Grid.SetColumn(settings, 3);
		inputBar.Children.Add(settings);

		var actionBar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(14, 0, 8, 0), Height = 38 };
		actionBar.Children.Add(_status);
		Grid.SetColumn(_hints, 1);
		actionBar.Children.Add(_hints);

		var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto") };
		Add(layout, inputBar, 0);
		Add(layout, Ui.Divider(), 1);
		Add(layout, new Panel { Children = { _list, _empty, _edit } }, 2);
		Add(layout, Ui.Divider(), 3);
		Add(layout, actionBar, 4);

		var frame = new Border
		{
			CornerRadius = AppTheme.WindowRadius,
			BorderThickness = new Thickness(1),
			ClipToBounds = true,
			Child = layout,
		};
		frame.Brush(Border.BackgroundProperty, Keys.Window).Brush(Border.BorderBrushProperty, Keys.Separator);
		Content = frame;

		AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
		_model.Changed += OnModelChanged;
		Render();
	}

	/// <summary>Raised when the popup hides itself (Escape, focus loss).</summary>
	public event EventHandler? Dismissed;

	public void FocusQuickAdd() => Dispatcher.UIThread.Post(() =>
	{
		_quickAdd.Focus();
		_quickAdd.SelectAll();
	});

	public void FocusList() => Dispatcher.UIThread.Post(() =>
	{
		if (_list.ItemCount == 0)
		{
			_quickAdd.Focus();
			return;
		}

		if (_list.SelectedIndex < 0)
		{
			_list.SelectedIndex = 0;
		}

		(_list.ContainerFromIndex(_list.SelectedIndex) as Control)?.Focus();
	});

	public void Dismiss()
	{
		_model.CancelPending();
		Hide();
		Dismissed?.Invoke(this, EventArgs.Empty);
	}

	protected override void OnClosed(EventArgs e)
	{
		_model.Changed -= OnModelChanged;
		base.OnClosed(e);
	}

	private static void Add(Grid grid, Control control, int row)
	{
		Grid.SetRow(control, row);
		grid.Children.Add(control);
	}

	private void OnModelChanged(object? sender, EventArgs e) => Render();

	private void Render()
	{
		_updating = true;
		try
		{
			_quickAddDue.SelectedIndex = Core.Input.DuePresets.All.ToList().IndexOf(_model.QuickAddDue);

			var editing = _model.Editing;
			_edit.Show(editing);
			_list.IsVisible = editing is null;
			_list.ItemsSource = _model.Rows;
			_list.SelectedItem = _model.Selected;
			_empty.IsVisible = editing is null && _model.Rows.Count == 0;
			_emptyTitle.Text = !_model.IsLoaded && !_model.IsFailed
				? DesktopText.Loading
				: _model.ShowCompleted ? DesktopText.EmptyCompleted : DesktopText.EmptyList;
			_emptyHint.IsVisible = _model.IsLoaded;

			RenderActionBar(editing is not null);
		}
		finally
		{
			_updating = false;
		}
	}

	private void RenderActionBar(bool editing)
	{
		var pending = _model.PendingDeleteId is { } pendingId ? _model.Rows.FirstOrDefault(r => r.Id == pendingId) : null;
		var error = _model.IsFailed ? DesktopText.StoreUnavailable : _model.Message;

		_status.Text = error
			?? (pending is not null
				? DesktopText.ConfirmDelete(pending.Todo.Title)
				: DesktopText.OpenFooter(_model.OpenCount));
		_status.Brush(TextBlock.ForegroundProperty, error is not null || pending is not null ? Keys.Red : Keys.LabelSecondary);
		ToolTip.SetTip(_status, _status.Text);

		_hints.Children.Clear();
		if (pending is not null)
		{
			_hints.Children.Add(KeyHint.Create(DesktopText.HintDelete, () => _ = _model.RequestDeleteAsync(pending.Id), KeyHint.Enter));
			_hints.Children.Add(KeyHint.Create(DesktopText.HintKeep, _model.CancelPending, KeyHint.Escape));
		}
		else if (editing)
		{
			_hints.Children.Add(KeyHint.Create(DesktopText.HintSave, () => _ = _edit.SaveAsync(), KeyHint.Enter));
			_hints.Children.Add(KeyHint.Create(DesktopText.HintCancel, _edit.Cancel, KeyHint.Escape));
		}
		else
		{
			if (_model.Selected is { } selected)
			{
				_hints.Children.Add(KeyHint.Create(DesktopText.HintEdit, () => _model.BeginEdit(selected.Id), KeyHint.Enter));
				_hints.Children.Add(KeyHint.Create(DesktopText.HintDone, () => _ = _model.ToggleAsync(selected.Id), KeyHint.Space));
			}

			var completed = KeyHint.Create(DesktopText.HintShowCompleted, () => _model.SetShowCompleted(!_model.ShowCompleted), KeyHint.Command, "D");
			if (_model.ShowCompleted)
			{
				completed.Classes.Add("active");
				ToolTip.SetTip(completed, DesktopText.ShowCompleted);
			}

			_hints.Children.Add(completed);
		}
	}

	private Grid Row(TodoRow row)
	{
		var check = CheckCircle.Create(row.Todo.IsCompleted, row.Status == Core.TodoStatus.Overdue, () => _model.ToggleAsync(row.Id));
		ToolTip.SetTip(check, row.Todo.IsCompleted ? DesktopText.Reopen : DesktopText.Complete);

		var title = Ui.Text(row.Todo.Title, Ui.Body, FontWeight.Normal, row.Todo.IsCompleted ? Keys.LabelTertiary : Keys.LabelPrimary);
		if (row.Todo.IsCompleted)
		{
			title.TextDecorations = TextDecorations.Strikethrough;
		}

		var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center, Children = { title } };
		if (!string.IsNullOrWhiteSpace(row.Todo.Description))
		{
			text.Children.Add(Ui.Text(row.Todo.Description.ReplaceLineEndings(" "), 11, brush: Keys.LabelTertiary));
		}

		var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), ColumnSpacing = 10, Background = Brushes.Transparent };
		grid.Children.Add(check);
		Grid.SetColumn(text, 1);
		grid.Children.Add(text);
		if (!string.IsNullOrEmpty(row.DueText))
		{
			var due = row.Status switch
			{
				Core.TodoStatus.Overdue => Ui.Tag(row.DueText, Keys.Red, Keys.RedTint),
				Core.TodoStatus.Due => Ui.Tag(row.DueText, Keys.Orange, Keys.OrangeTint),
				_ => Ui.Tag(row.DueText, Keys.LabelSecondary, null),
			};
			Grid.SetColumn(due, 2);
			grid.Children.Add(due);
		}

		var menu = new ContextMenu();
		menu.Items.Add(MenuItem(DesktopText.Edit, () => _model.BeginEdit(row.Id)));
		menu.Items.Add(MenuItem(row.Todo.IsCompleted ? DesktopText.Reopen : DesktopText.Complete, () => _ = _model.ToggleAsync(row.Id)));
		if (_model.Settings.DefaultSort == Core.TodoSortOrder.Manual && !row.Todo.IsCompleted)
		{
			menu.Items.Add(MenuItem(DesktopText.MoveUp, () => _ = _model.MoveAsync(row.Id, -1)));
			menu.Items.Add(MenuItem(DesktopText.MoveDown, () => _ = _model.MoveAsync(row.Id, 1)));
		}

		menu.Items.Add(new Separator());
		var delete = MenuItem(DesktopText.Delete, () => _ = _model.RequestDeleteAsync(row.Id));
		delete.Brush(TemplatedControl.ForegroundProperty, Keys.Red);
		menu.Items.Add(delete);
		grid.ContextMenu = menu;
		return grid;
	}

	private static MenuItem MenuItem(string header, Action action)
	{
		var item = new MenuItem { Header = header };
		item.Click += (_, _) => action();
		return item;
	}

	private bool IsInList(IInputElement? element)
	{
		for (var visual = element as Visual; visual is not null; visual = visual.GetVisualParent())
		{
			if (ReferenceEquals(visual, _list))
			{
				return true;
			}
		}

		return false;
	}

	private void EditSelected()
	{
		if (_model.Selected is { } row)
		{
			_model.BeginEdit(row.Id);
		}
	}

	private async void OnKeyDown(object? sender, KeyEventArgs e)
	{
		// Store failures become model messages; anything else reaches the dispatcher handler, which logs it.
		var command = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
		var focused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
		var inText = focused is TextBox;

		if (e.Key == Key.Escape)
		{
			e.Handled = true;
			if (_model.PendingDeleteId is not null || _model.EditingId is not null)
			{
				_model.CancelPending();
				FocusList();
			}
			else
			{
				Dismiss();
			}

			return;
		}

		if (e.KeyModifiers == command)
		{
			switch (e.Key)
			{
				case Key.N:
					e.Handled = true;
					_model.CancelPending();
					FocusQuickAdd();
					return;
				case Key.D when _model.EditingId is null:
					e.Handled = true;
					_model.SetShowCompleted(!_model.ShowCompleted);
					return;
				case Key.OemComma:
					e.Handled = true;
					_openSettings();
					return;
			}
		}

		if (_model.EditingId is not null)
		{
			return;
		}

		if (_quickAdd.IsFocused)
		{
			if (e.Key == Key.Enter)
			{
				e.Handled = true;

				// Cleared before awaiting so a quick second Enter cannot add the same todo twice.
				var title = _quickAdd.Text ?? string.Empty;
				_quickAdd.Text = string.Empty;
				if (!await _model.CreateAsync(title, _model.QuickAddDue) && string.IsNullOrEmpty(_quickAdd.Text))
				{
					_quickAdd.Text = title;
				}
			}
			else if (e.Key == Key.Down)
			{
				e.Handled = true;
				FocusList();
			}

			return;
		}

		// Row shortcuts apply only while the list has focus, so Enter or Space on a button does its own job.
		if (inText || !IsInList(focused) || _model.Selected is not { } selected)
		{
			return;
		}

		switch (e.Key)
		{
			case Key.Space:
				e.Handled = true;
				await _model.ToggleAsync(selected.Id);
				FocusList();
				break;
			case Key.Enter when _model.PendingDeleteId == selected.Id:
				e.Handled = true;
				await _model.RequestDeleteAsync(selected.Id);
				FocusList();
				break;
			case Key.Enter:
				e.Handled = true;
				_model.BeginEdit(selected.Id);
				break;
			case Key.Delete or Key.Back:
				e.Handled = true;
				await _model.RequestDeleteAsync(selected.Id);
				FocusList();
				break;
			case Key.Up when _list.SelectedIndex == 0:
				e.Handled = true;
				FocusQuickAdd();
				break;
		}
	}
}
