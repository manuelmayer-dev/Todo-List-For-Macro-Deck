using System.Globalization;
using MacroDeck.Localization;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Runtime;
using TodoList.Core;
using TodoList.Core.Input;

namespace TodoList.Plugin.Ui;

/// <summary>
/// The todo list modal. This SDK version offers single-line text fields, no date picker and no nested
/// dialogs, so due dates are typed or picked from presets and deleting asks for a second tap in place.
/// </summary>
internal sealed class TodoModalView
{
	public const string ViewId = "todo-list";

	private const int OpenTab = 0;
	private const int DueTab = 1;
	private const int CompletedTab = 2;
	private const long DuplicateWindowMilliseconds = 2000;

	private readonly TodoUiState _state;
	private readonly ITodoCommands _commands;
	private readonly UiState<int> _tab = new(OpenTab);
	private readonly UiState<string> _draft = new(string.Empty);
	private readonly UiState<LocalizedText?> _message = new(null);
	private readonly UiState<Guid?> _confirmDelete = new(null);
	private readonly UiState<Guid?> _editing = new(null);
	private readonly UiState<string> _editTitle = new(string.Empty);
	private readonly UiState<string> _editNotes = new(string.Empty);
	private readonly UiState<string> _editDue = new(string.Empty);
	private string? _lastCreatedTitle;
	private long _lastCreatedAt;

	private TodoModalView(TodoUiState state, ITodoCommands commands)
	{
		_state = state;
		_commands = commands;
	}

	public static UiElement Build(TodoUiState state, ITodoCommands commands) => new TodoModalView(state, commands).Root();

	internal IReadOnlyList<TodoItem> Items()
	{
		var settings = _state.Settings.Value;
		var now = _state.Now.Value;
		var snapshot = _state.Snapshot.Value;
		return _tab.Value switch
		{
			CompletedTab => [.. snapshot.Completed.OrderByDescending(t => t.CompletedAt)],
			DueTab => snapshot.Query(settings.DefaultSort, overdueFirst: true, includeCompleted: false, TodoStatusFilter.DueOrOverdue, now, settings.Thresholds),
			_ => snapshot.Query(settings.DefaultSort, overdueFirst: true, includeCompleted: false, TodoStatusFilter.All, now, settings.Thresholds),
		};
	}

	private UiStack Root() => new UiStack
	{
		Key = "modal",
		Direction = UiComponentDirections.Vertical,
		Gap = UiSize.FromBasis(0.02),
		Padding = UiSize.FromBasis(0.03),
		Children =
		[
			new UiWhen
			{
				Key = "status-when",
				Condition = () => _state.Status.Value != TodoStoreStatus.Ready,
				Content = () => Text("status", UiText.FromLocalized(() => _state.Status.Value == TodoStoreStatus.Failed
					? Strings.Errors.StoreUnavailable()
					: Strings.Widget.Loading()), 0.04, UiComponentTextRoles.Secondary),
			},
			new UiWhen
			{
				Key = "list-when",
				Condition = () => _state.Status.Value == TodoStoreStatus.Ready && _editing.Value is null,
				Content = ListPane,
			},
			new UiWhen
			{
				Key = "edit-when",
				Condition = () => _state.Status.Value == TodoStoreStatus.Ready && _editing.Value is not null,
				Content = EditPane,
			},
			new UiWhen
			{
				Key = "message-when",
				Condition = () => _message.Value is not null,
				Content = () => new UiTextRun
				{
					Key = "message",
					Text = UiText.Optional(() => _message.Value is { } message ? message : UiText.None()),
					Size = UiSize.FromBasis(0.035),
					Color = TodoLabels.OverdueColor,
					MaxLines = 2,
					Wrap = true,
				},
			},
			new UiButton
			{
				Key = "done",
				Answer = "done",
				MainSize = UiSize.FromBasis(0.09),
				Justify = UiComponentJustify.Center,
				Align = UiComponentAlignments.Center,
				Events = [UiEventHandler.On(UiComponentEvents.Press, () => { })],
				Children = [Text("done-label", Strings.Modal.Done(), 0.04, UiComponentTextRoles.Primary, UiComponentTextWeights.SemiBold)],
			},
		],
	};

	private UiStack ListPane() => new UiStack
	{
		Key = "list-pane",
		Direction = UiComponentDirections.Vertical,
		Fill = true,
		Gap = UiSize.FromBasis(0.02),
		Children =
		[
			new UiSegmented
			{
				Key = "tabs",
				MainSize = UiSize.FromBasis(0.08),
				Selected = UiValue.From(() => _tab.Value),
				Events = [UiEventHandler.On(UiComponentEvents.Change, SelectTab)],
				Children =
				[
					Text("tab-open", UiText.FromLocalized(() => Strings.Modal.Tabs.Open(Counts().Open.ToString(CultureInfo.InvariantCulture))), 0.032),
					Text("tab-due", UiText.FromLocalized(() => Strings.Modal.Tabs.Due((Counts().Due + Counts().Overdue).ToString(CultureInfo.InvariantCulture))), 0.032),
					Text("tab-completed", UiText.FromLocalized(() => Strings.Modal.Tabs.Completed(Counts().Completed.ToString(CultureInfo.InvariantCulture))), 0.032),
				],
				Fallback = new UiStack
				{
					Key = "tabs-fallback",
					Direction = UiComponentDirections.Horizontal,
					MainSize = UiSize.FromBasis(0.08),
					Gap = UiSize.FromBasis(0.02),
					Children =
					[
						TabButton("tab-open-fallback", OpenTab, Strings.Modal.Tabs.OpenShort()),
						TabButton("tab-due-fallback", DueTab, Strings.Modal.Tabs.DueShort()),
						TabButton("tab-completed-fallback", CompletedTab, Strings.Modal.Tabs.CompletedShort()),
					],
				},
			},
			new UiStack
			{
				Key = "add",
				Direction = UiComponentDirections.Horizontal,
				Align = UiComponentAlignments.Center,
				MainSize = UiSize.FromBasis(0.09),
				Gap = UiSize.FromBasis(0.02),
				Children =
				[
					new UiTextField
					{
						Key = "add-title",
						Fill = true,
						Size = UiSize.FromBasis(0.04),
						Text = UiValue.From(() => _draft.Value),
						Placeholder = Strings.Modal.AddPlaceholder(),
						Events =
						[
							UiEventHandler.On(UiComponentEvents.Adjust, data =>
							{
								if (data.TryGetString(out var text))
								{
									_draft.Set(text);
								}
							}),
							// Change arrives on Enter and when the field loses focus; either commits the typed title.
							UiEventHandler.OnAsync(UiComponentEvents.Change, async (data, cancellationToken) =>
							{
								if (data.TryGetString(out var text))
								{
									_draft.Set(text);
								}

								await CreateAsync(cancellationToken);
							}),
						],
					},
					IconButton("add-button", UiIcons.Plus, 0.09, CreateAsync),
				],
			},
			new UiList
			{
				Key = "list",
				Fill = true,
				Gap = UiSize.FromBasis(0.012),
				Children =
				[
					new UiWhen
					{
						Key = "empty-when",
						Condition = () => Items().Count == 0,
						Content = () => Text("empty", UiText.FromLocalized(() => _tab.Value switch
						{
							CompletedTab => Strings.Modal.EmptyCompleted(),
							DueTab => Strings.Modal.EmptyDue(),
							_ => Strings.Modal.EmptyOpen(),
						}), 0.038, UiComponentTextRoles.Muted),
					},
					new UiRepeat<TodoItem>
					{
						Key = "rows",
						Items = UiValue.From(Items),
						KeySelector = todo => todo.Id.ToString("N"),
						Template = Row,
					},
				],
			},
		],
	};

	private UiStack Row(TodoItem todo, string key) => new UiStack
	{
		Key = key,
		Direction = UiComponentDirections.Horizontal,
		Align = UiComponentAlignments.Center,
		MainSize = UiSize.FromBasis(0.1),
		Gap = UiSize.FromBasis(0.02),
		Children =
		[
			new UiButton
			{
				Key = "toggle",
				MainSize = UiSize.FromBasis(0.08),
				Background = "transparent",
				Justify = UiComponentJustify.Center,
				Align = UiComponentAlignments.Center,
				Events =
				[
					UiEventHandler.OnAsync(UiComponentEvents.Press, cancellationToken =>
						RunAsync(() => _commands.SetCompletedAsync(todo.Id, !todo.IsCompleted, cancellationToken))),
				],
				Children = [TodoWidgetView.Marker(_state, todo, "marker", 0.045)],
			},
			new UiButton
			{
				Key = "open",
				Fill = true,
				Background = "transparent",
				Direction = UiComponentDirections.Vertical,
				Justify = UiComponentJustify.Center,
				Events = [UiEventHandler.On(UiComponentEvents.Press, () => BeginEdit(todo))],
				Children =
				[
					new UiTextRun
					{
						Key = "title",
						Text = todo.Title,
						Size = UiSize.FromBasis(0.04),
						Weight = UiComponentTextWeights.Medium,
						Role = todo.IsCompleted ? UiComponentTextRoles.Muted : UiComponentTextRoles.Primary,
						MaxLines = 1,
					},
					new UiTextRun
					{
						Key = "due",
						Text = UiText.Optional(() => todo.DueAt is null
							? (todo.Description is { } notes ? notes : UiText.None())
							: TodoLabels.Due(todo, Status(todo), _state.Now.Value)),
						Size = UiSize.FromBasis(0.03),
						Color = UiValue.Optional(() => TodoLabels.DueColorFor(Status(todo)) is { } color ? UiValue.Of(color) : UiValue.None<string>()),
						Role = UiComponentTextRoles.Muted,
						MaxLines = 1,
					},
				],
			},
			new UiButton
			{
				Key = "delete",
				MainSize = UiSize.FromBasis(0.08),
				Background = "transparent",
				Justify = UiComponentJustify.Center,
				Align = UiComponentAlignments.Center,
				Events = [UiEventHandler.OnAsync(UiComponentEvents.Press, cancellationToken => DeleteAsync(todo.Id, cancellationToken))],
				Children =
				[
					new UiIcon
					{
						Key = "delete-icon",
						Icon = UiValue.From(() => _confirmDelete.Value == todo.Id ? UiIcons.AlertTriangle : UiIcons.Trash),
						Color = UiValue.Optional(() => _confirmDelete.Value == todo.Id ? UiValue.Of(TodoLabels.OverdueColor) : UiValue.None<string>()),
						Role = UiComponentTextRoles.Muted,
						Size = UiSize.FromBasis(0.04),
					},
				],
			},
		],
	};

	private UiStack EditPane() => new UiStack
	{
		Key = "edit-pane",
		Direction = UiComponentDirections.Vertical,
		Fill = true,
		Gap = UiSize.FromBasis(0.02),
		Children =
		[
			Text("edit-heading", Strings.Modal.Edit.Heading(), 0.045, UiComponentTextRoles.Primary, UiComponentTextWeights.SemiBold),
			Field("edit-title", Strings.Modal.Edit.Title(), _editTitle, Strings.Modal.AddPlaceholder()),
			Field("edit-notes", Strings.Modal.Edit.Notes(), _editNotes, Strings.Modal.Edit.NotesPlaceholder()),
			Field("edit-due", Strings.Modal.Edit.Due(), _editDue, Strings.Modal.Edit.DuePlaceholder()),
			new UiStack
			{
				Key = "edit-presets",
				Direction = UiComponentDirections.Horizontal,
				MainSize = UiSize.FromBasis(0.075),
				Gap = UiSize.FromBasis(0.015),
				Children =
				[
					PresetButton("preset-hour", DuePreset.InOneHour, Strings.Presets.InOneHour()),
					PresetButton("preset-evening", DuePreset.TodayEvening, Strings.Presets.TodayEvening()),
					PresetButton("preset-tomorrow", DuePreset.TomorrowMorning, Strings.Presets.TomorrowMorning()),
					PresetButton("preset-week", DuePreset.NextWeek, Strings.Presets.NextWeek()),
					PresetButton("preset-none", DuePreset.None, Strings.Presets.None()),
				],
			},
			new UiStack
			{
				Key = "edit-shift",
				Direction = UiComponentDirections.Horizontal,
				MainSize = UiSize.FromBasis(0.075),
				Gap = UiSize.FromBasis(0.015),
				Children =
				[
					ShiftButton("shift-day-back", Strings.Modal.Edit.DayEarlier(), TimeSpan.FromDays(-1)),
					ShiftButton("shift-day-forward", Strings.Modal.Edit.DayLater(), TimeSpan.FromDays(1)),
					ShiftButton("shift-hour-back", Strings.Modal.Edit.HourEarlier(), TimeSpan.FromHours(-1)),
					ShiftButton("shift-hour-forward", Strings.Modal.Edit.HourLater(), TimeSpan.FromHours(1)),
				],
			},
			new UiStack
			{
				Key = "edit-buttons",
				Direction = UiComponentDirections.Horizontal,
				MainSize = UiSize.FromBasis(0.09),
				Gap = UiSize.FromBasis(0.02),
				Children =
				[
					TextButton("edit-cancel", MacroDeckStrings.Common.Cancel(), () => EndEdit()),
					new UiButton
					{
						Key = "edit-save",
						Fill = true,
						Justify = UiComponentJustify.Center,
						Align = UiComponentAlignments.Center,
						Events = [UiEventHandler.OnAsync(UiComponentEvents.Press, SaveAsync)],
						Children = [Text("edit-save-label", MacroDeckStrings.Common.Save(), 0.038, UiComponentTextRoles.Primary, UiComponentTextWeights.SemiBold)],
					},
				],
			},
		],
	};

	private static UiStack Field(string key, UiText label, UiState<string> value, UiText placeholder) => new UiStack
	{
		Key = key,
		Direction = UiComponentDirections.Vertical,
		Gap = UiSize.FromBasis(0.008),
		Children =
		[
			Text("label", label, 0.03, UiComponentTextRoles.Secondary),
			new UiTextField
			{
				Key = "input",
				MainSize = UiSize.FromBasis(0.08),
				Size = UiSize.FromBasis(0.038),
				Text = UiValue.From(() => value.Value),
				Placeholder = placeholder,
				Events =
				[
					UiEventHandler.On(UiComponentEvents.Adjust, data => Store(data, value)),
					UiEventHandler.On(UiComponentEvents.Change, data => Store(data, value)),
				],
			},
		],
	};

	private UiButton PresetButton(string key, DuePreset preset, UiText label) => TextButton(key, label, () =>
	{
		var due = DuePresets.Resolve(preset, _state.Now.Value, TimeZoneInfo.Local);
		_editDue.Set(due is { } value ? DueInputParser.FormatForEditing(value, TimeZoneInfo.Local) : string.Empty);
	});

	private UiButton ShiftButton(string key, UiText label, TimeSpan by) => TextButton(key, label, () =>
	{
		if (!TryParseDue(out var due))
		{
			_message.Set(Strings.Errors.InvalidDueDate());
			return;
		}

		var basis = due ?? DuePresets.Resolve(DuePreset.TomorrowMorning, _state.Now.Value, TimeZoneInfo.Local)!.Value;
		var shifted = LocalTime.ToUtc(LocalTime.ToWallClock(basis, TimeZoneInfo.Local).Add(by), TimeZoneInfo.Local);
		_editDue.Set(DueInputParser.FormatForEditing(shifted, TimeZoneInfo.Local));
		_message.Set(null);
	});

	private static UiButton TextButton(string key, UiText label, Action press) => new()
	{
		Key = key,
		Fill = true,
		Justify = UiComponentJustify.Center,
		Align = UiComponentAlignments.Center,
		Events = [UiEventHandler.On(UiComponentEvents.Press, press)],
		Children = [Text("label", label, 0.03, UiComponentTextRoles.Primary)],
	};

	private UiButton TabButton(string key, int tab, UiText label) => TextButton(key, label, () => _tab.Set(tab));

	private static UiButton IconButton(string key, string icon, double size, Func<CancellationToken, Task> press) => new()
	{
		Key = key,
		MainSize = UiSize.FromBasis(size),
		Justify = UiComponentJustify.Center,
		Align = UiComponentAlignments.Center,
		Events = [UiEventHandler.OnAsync(UiComponentEvents.Press, press)],
		Children = [new UiIcon { Key = "icon", Icon = icon, Size = UiSize.FromBasis(size * 0.5), Role = UiComponentTextRoles.Primary }],
	};

	private static UiTextRun Text(string key, UiText text, double size, string? role = null, string? weight = null) => new()
	{
		Key = key,
		Text = text,
		Size = UiSize.FromBasis(size),
		Role = role ?? UiComponentTextRoles.Primary,
		Weight = weight ?? UiComponentTextWeights.Regular,
		MaxLines = 1,
	};

	private static void Store(UiEventData data, UiState<string> target)
	{
		if (data.TryGetString(out var text))
		{
			target.Set(text);
		}
	}

	private UiEventOutcome SelectTab(UiEventData data)
	{
		if (!data.TryGetDouble(out var index) || index is < OpenTab or > CompletedTab)
		{
			return UiEventOutcome.Rejected("The tab index is not a number between 0 and 2.");
		}

		_tab.Set((int)index);
		_confirmDelete.Set(null);
		return UiEventOutcome.Accepted;
	}

	private async Task CreateAsync(CancellationToken cancellationToken)
	{
		// Enter, leaving the field and the add button can all arrive for one title, and handlers run
		// concurrently, so the title is taken and cleared before anything is awaited.
		var title = _draft.Peek().Trim();
		if (title.Length == 0)
		{
			return;
		}

		_draft.Set(string.Empty);

		// A client can still send the old text when the field loses focus right after Enter.
		if (title == _lastCreatedTitle && Environment.TickCount64 - _lastCreatedAt < DuplicateWindowMilliseconds)
		{
			return;
		}

		_lastCreatedTitle = title;
		_lastCreatedAt = Environment.TickCount64;
		var due = DuePresets.Resolve(_state.Settings.Peek().DefaultDue, _state.Now.Peek(), TimeZoneInfo.Local);
		var failure = await _commands.CreateAsync(new TodoDraft(title, DueAt: due), cancellationToken);
		if (failure is null)
		{
			if (_tab.Peek() == CompletedTab)
			{
				_tab.Set(OpenTab);
			}
		}
		else
		{
			_lastCreatedTitle = null;
			if (_draft.Peek().Length == 0)
			{
				_draft.Set(title);
			}
		}

		_message.Set(failure);
	}

	private async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
	{
		if (_state.Settings.Peek().ConfirmDelete && _confirmDelete.Peek() != id)
		{
			_confirmDelete.Set(id);
			return;
		}

		_confirmDelete.Set(null);
		await RunAsync(() => _commands.DeleteAsync(id, cancellationToken));
	}

	private void BeginEdit(TodoItem todo)
	{
		_confirmDelete.Set(null);
		_message.Set(null);
		_editTitle.Set(todo.Title);
		_editNotes.Set(todo.Description ?? string.Empty);
		_editDue.Set(todo.DueAt is { } due ? DueInputParser.FormatForEditing(due, TimeZoneInfo.Local) : string.Empty);
		_editing.Set(todo.Id);
	}

	private void EndEdit()
	{
		_editing.Set(null);
		_message.Set(null);
	}

	private async Task SaveAsync(CancellationToken cancellationToken)
	{
		if (_editing.Peek() is not { } id)
		{
			return;
		}

		if (!TryParseDue(out var due))
		{
			_message.Set(Strings.Errors.InvalidDueDate());
			return;
		}

		var failure = await _commands.UpdateAsync(id, new TodoDraft(_editTitle.Peek(), _editNotes.Peek(), due), cancellationToken);
		if (failure is null)
		{
			EndEdit();
		}
		else
		{
			_message.Set(failure);
		}
	}

	private bool TryParseDue(out DateTimeOffset? due) =>
		DueInputParser.TryParse(_editDue.Peek(), CultureInfo.CurrentCulture, _state.Now.Peek(), TimeZoneInfo.Local, out due);

	private async Task RunAsync(Func<Task<LocalizedText?>> command) => _message.Set(await command());

	private TodoStatus Status(TodoItem todo) => TodoStatusRules.Evaluate(todo, _state.Now.Value, _state.Thresholds);

	private TodoCounts Counts() => _state.Snapshot.Value.Count(_state.Now.Value, _state.Thresholds);
}
