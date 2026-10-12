using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using TodoList.Core;
using TodoList.Core.Input;
using TodoList.Desktop.Localization;
using TodoList.Desktop.Theme;
using TodoList.Desktop.ViewModels;

namespace TodoList.Desktop.Views;

/// <summary>
/// The inline editor for one todo, laid out like a launcher form: labels on the left, fields on the right.
/// The due date is typed in words, stepped through with the arrow keys, picked from the calendar or from
/// the preset buttons. Enter in the title or due field saves, Escape cancels.
/// </summary>
internal sealed class EditPanel : ScrollViewer
{
	private readonly TodoListViewModel _model;
	private readonly Action _afterClose;
	private readonly TextBox _title = new();
	private readonly TextBox _notes = new() { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 72, PlaceholderText = DesktopText.EditNotes };
	private readonly DueField _due = new();
	private Guid? _shownId;

	public EditPanel(TodoListViewModel model, Action afterClose)
	{
		_model = model;
		_afterClose = afterClose;
		IsVisible = false;

		var presets = new WrapPanel { Orientation = Orientation.Horizontal, ItemSpacing = 6, LineSpacing = 6 };
		foreach (var preset in DuePresets.All.Where(p => p != DuePreset.None).Append(DuePreset.None))
		{
			presets.Children.Add(Ui.Button(Ui.PresetLabel(preset), () => _due.SetPreset(preset)));
		}

		var form = new Grid
		{
			ColumnDefinitions = new ColumnDefinitions("72,*"),
			RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto"),
			ColumnSpacing = 12,
			RowSpacing = 10,
			Margin = new Thickness(16, 16, 16, 12),
		};
		AddField(form, 0, DesktopText.EditTitle, _title);
		AddField(form, 1, DesktopText.EditNotes, _notes);
		AddField(form, 2, DesktopText.EditDueDate, _due);
		Place(form, presets, 3);
		Content = form;

		AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
	}

	/// <summary>Shows the editor for <paramref name="todo"/>, or hides it for null.</summary>
	public void Show(TodoItem? todo)
	{
		IsVisible = todo is not null;
		if (todo is null || todo.Id == _shownId)
		{
			_shownId = todo?.Id;
			return;
		}

		_shownId = todo.Id;
		_title.Text = todo.Title;
		_notes.Text = todo.Description;
		_due.SetDue(todo.DueAt);
		Dispatcher.UIThread.Post(() =>
		{
			_title.Focus();
			_title.SelectAll();
		});
	}

	public async Task SaveAsync()
	{
		if (!_due.TryGetDue(out var dueAt))
		{
			_model.ShowMessage(DesktopText.InvalidDue);
			_due.Focus();
			return;
		}

		if (await _model.SaveEditAsync(_title.Text ?? string.Empty, _notes.Text, dueAt))
		{
			_shownId = null;
			_afterClose();
		}
	}

	public void Cancel()
	{
		_shownId = null;
		_model.CancelPending();
		_afterClose();
	}

	private static void AddField(Grid form, int row, string label, Control field)
	{
		var text = Ui.Text(label, Ui.Body, FontWeight.Normal, Keys.LabelSecondary);
		text.HorizontalAlignment = HorizontalAlignment.Right;
		text.VerticalAlignment = field is TextBox { AcceptsReturn: true } ? VerticalAlignment.Top : VerticalAlignment.Center;
		text.Margin = field is TextBox { AcceptsReturn: true } ? new Thickness(0, 7, 0, 0) : default;
		Grid.SetRow(text, row);
		form.Children.Add(text);
		Place(form, field, row);
	}

	private static void Place(Grid form, Control control, int row)
	{
		Grid.SetRow(control, row);
		Grid.SetColumn(control, 1);
		form.Children.Add(control);
	}

	private void OnKeyDown(object? sender, KeyEventArgs e)
	{
		var commandEnter = e.Key == Key.Enter && e.KeyModifiers == (OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control);
		if (e.Key == Key.Enter && (commandEnter || !_notes.IsFocused))
		{
			e.Handled = true;
			_ = SaveAsync();
		}
	}
}
