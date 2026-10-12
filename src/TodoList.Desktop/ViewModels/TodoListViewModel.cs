using System.Globalization;
using Serilog;
using TodoList.Core;
using TodoList.Core.Input;
using TodoList.Core.Settings;
using TodoList.Core.Storage;
using TodoList.Desktop.Localization;

namespace TodoList.Desktop.ViewModels;

/// <summary>
/// State and operations behind the tray popup and the quick-add window. It never touches controls; the views
/// listen to <see cref="Changed"/>. All members are used on the UI thread.
/// </summary>
internal sealed class TodoListViewModel : IDisposable
{
	private readonly TodoService _todos;
	private readonly SettingsService _settings;
	private readonly TimeProvider _clock;
	private readonly ILogger _logger;
	private readonly Action<Action> _post;

	public TodoListViewModel(TodoService todos, SettingsService settings, TimeProvider clock, ILogger logger, Action<Action> post)
	{
		_todos = todos;
		_settings = settings;
		_clock = clock;
		_logger = logger.ForContext<TodoListViewModel>();
		_post = post;
		ShowCompleted = settings.Current.ShowCompleted;
		QuickAddDue = settings.Current.DefaultDue;
		_todos.Changed += OnStoreChanged;
		_settings.Changed += OnSettingsChanged;
		Refresh();
	}

	public event EventHandler? Changed;

	public IReadOnlyList<TodoRow> Rows { get; private set; } = [];

	public int OpenCount { get; private set; }

	public bool IsLoaded => _todos.Ready.IsCompletedSuccessfully;

	public bool IsFailed => _todos.Ready.IsFaulted;

	public bool ShowCompleted { get; private set; }

	public Guid? SelectedId { get; set; }

	public Guid? PendingDeleteId { get; private set; }

	public Guid? EditingId { get; private set; }

	public DuePreset QuickAddDue { get; set; }

	public string? Message { get; private set; }

	public TodoSettings Settings => _settings.Current;

	/// <summary>The quick-add shortcut in the platform's notation, or empty while it is turned off.</summary>
	public string ShortcutHint => Settings.HotkeyEnabled && Core.Hotkeys.HotkeyGesture.TryParse(Settings.Hotkey, out var gesture)
		? DesktopText.ShortcutHint(gesture.ToDisplayString(OperatingSystem.IsMacOS()))
		: string.Empty;

	public TodoRow? Selected => Rows.FirstOrDefault(r => r.Id == SelectedId);

	public TodoItem? Editing => EditingId is { } id ? _todos.Snapshot.Find(id) : null;

	/// <summary>Called when the popup is shown again; forgets transient state unless the user wants it kept.</summary>
	public void Reset()
	{
		// Shown again after a while: pick up a time zone change for the local due labels.
		TimeZoneInfo.ClearCachedData();
		PendingDeleteId = null;
		Message = null;
		EditingId = null;
		if (!Settings.RememberPopupState)
		{
			ShowCompleted = Settings.ShowCompleted;
			SelectedId = null;
			QuickAddDue = Settings.DefaultDue;
		}

		Refresh();
	}

	public void SetShowCompleted(bool value)
	{
		ShowCompleted = value;
		Refresh();
	}

	public void Refresh()
	{
		var now = _clock.GetUtcNow();
		var settings = Settings;
		var snapshot = _todos.Snapshot;
		var culture = CultureInfo.CurrentCulture;
		Rows = [.. snapshot
			.Query(settings.DefaultSort, overdueFirst: true, ShowCompleted, TodoStatusFilter.All, now, settings.Thresholds)
			.Select(todo =>
			{
				var status = TodoStatusRules.Evaluate(todo, now, settings.Thresholds);
				return new TodoRow(todo, status, DueFormatter.Format(todo, status, now, TimeZoneInfo.Local, culture));
			})];
		OpenCount = snapshot.Count(now, settings.Thresholds).Open;
		if (SelectedId is { } id && Rows.All(r => r.Id != id))
		{
			SelectedId = null;
		}

		Changed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Creates a todo from the quick-add text. Returns true when one was created.</summary>
	public Task<bool> CreateAsync(string title, DuePreset due) =>
		CreateAsync(title, DuePresets.Resolve(due, _clock.GetUtcNow(), TimeZoneInfo.Local));

	/// <summary>Creates a todo with an explicit due date. Returns true when one was created.</summary>
	public async Task<bool> CreateAsync(string title, DateTimeOffset? dueAt)
	{
		if (string.IsNullOrWhiteSpace(title))
		{
			return false;
		}

		var created = await RunAsync(async () => await _todos.CreateAsync(new TodoDraft(title, DueAt: dueAt), CancellationToken.None));
		if (created is not null)
		{
			SelectedId = created.Id;
			Refresh();
		}

		return created is not null;
	}

	public Task ToggleAsync(Guid id) => RunAsync(async () =>
	{
		var todo = _todos.Snapshot.Find(id);
		return todo is null
			? null
			: todo.IsCompleted
				? await _todos.ReopenAsync(id, CancellationToken.None)
				: await _todos.CompleteAsync(id, CancellationToken.None);
	});

	/// <summary>Asks for confirmation first when the setting wants it; a second request deletes.</summary>
	public async Task RequestDeleteAsync(Guid id)
	{
		if (Settings.ConfirmDelete && PendingDeleteId != id)
		{
			PendingDeleteId = id;
			Changed?.Invoke(this, EventArgs.Empty);
			return;
		}

		PendingDeleteId = null;
		var index = Rows.ToList().FindIndex(r => r.Id == id);
		await RunAsync(() => _todos.DeleteAsync(id, CancellationToken.None));
		if (Rows.Count > 0 && index >= 0)
		{
			SelectedId = Rows[Math.Min(index, Rows.Count - 1)].Id;
			Changed?.Invoke(this, EventArgs.Empty);
		}
	}

	public void CancelPending()
	{
		PendingDeleteId = null;
		EditingId = null;
		Message = null;
		Changed?.Invoke(this, EventArgs.Empty);
	}

	public void BeginEdit(Guid id)
	{
		PendingDeleteId = null;
		Message = null;
		EditingId = _todos.Snapshot.Find(id) is null ? null : id;
		Changed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Saves the edit. Returns false and keeps editing when the store or validation refuses it.</summary>
	public async Task<bool> SaveEditAsync(string title, string? notes, DateTimeOffset? dueAt)
	{
		if (EditingId is not { } id)
		{
			return false;
		}

		var updated = await RunAsync(() => _todos.UpdateAsync(id, new TodoDraft(title, notes, dueAt), CancellationToken.None));
		if (updated is null)
		{
			return false;
		}

		EditingId = null;
		Changed?.Invoke(this, EventArgs.Empty);
		return true;
	}

	/// <summary>Shows a message in the action bar, for input the view rejects before saving.</summary>
	public void ShowMessage(string message)
	{
		Message = message;
		Changed?.Invoke(this, EventArgs.Empty);
	}

	/// <summary>Moves an open todo within the manual order; only meaningful while sorting manually.</summary>
	public Task MoveAsync(Guid id, int delta)
	{
		var index = Rows.Where(r => !r.Todo.IsCompleted).Select(r => r.Id).ToList().IndexOf(id);
		if (index < 0 || Settings.DefaultSort != TodoSortOrder.Manual)
		{
			return Task.CompletedTask;
		}

		return RunAsync(async () =>
		{
			await _todos.MoveAsync(id, index + delta, CancellationToken.None);
			return _todos.Snapshot.Find(id);
		});
	}

	public void Dispose()
	{
		_todos.Changed -= OnStoreChanged;
		_settings.Changed -= OnSettingsChanged;
	}

	private async Task<TodoItem?> RunAsync(Func<Task<TodoItem?>> operation)
	{
		try
		{
			var result = await operation();
			Message = result is null ? DesktopText.TodoNotFound : null;
			return result;
		}
		catch (TodoValidationException exception)
		{
			Message = exception.Error switch
			{
				TodoValidationError.TitleRequired => DesktopText.TitleRequired,
				TodoValidationError.TitleTooLong => DesktopText.TitleTooLong(TodoValidation.MaxTitleLength),
				_ => DesktopText.DescriptionTooLong(TodoValidation.MaxDescriptionLength),
			};
			return null;
		}
		catch (TodoStoreException exception)
		{
			_logger.Error(exception, "A desktop change could not be saved");
			Message = DesktopText.StoreFailed;
			return null;
		}
		finally
		{
			Changed?.Invoke(this, EventArgs.Empty);
		}
	}

	// The store raises changes on whichever thread wrote; the views live on the UI thread.
	private void OnStoreChanged(object? sender, TodoChangedEventArgs change) => _post(Refresh);

	private void OnSettingsChanged(object? sender, TodoSettings settings) => _post(Refresh);
}
