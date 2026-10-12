using System.Globalization;
using MacroDeck.Ui.Components;
using MacroDeck.Ui.Dsl;
using MacroDeck.Ui.Runtime;
using TodoList.Core;

namespace TodoList.Plugin.Ui;

/// <summary>
/// The todo widget. The SDK does not tell a widget its size, so one tree carries a layout per tile shape and
/// the reader picks by cells: 1x1 shows the count, 2x1 and 1x2 a few rows, 2x2 and larger a paged list with
/// complete buttons. Layouts without controls leave the tap to the widget's default action (the modal).
/// </summary>
internal static class TodoWidgetView
{
	private const double Inset = 0.08;

	public static UiElement Build(TodoUiState state, TodoWidgetSettings settings, ITodoCommands commands)
	{
		var page = new UiState<int>(0);
		return new UiResponsive
		{
			Key = "todo-widget",
			Default = Compact(state),
			Variants =
			[
				new UiResponsiveVariant { MinWidth = 2, MinHeight = 2, Content = Large(state, settings, commands, page) },
				new UiResponsiveVariant { MinWidth = 2, Content = Rows(state, settings, "wide", maxRows: 3, header: 0.26, new RowStyle(0.22, 0.14, Stacked: false)) },
				new UiResponsiveVariant { MinHeight = 2, Content = Rows(state, settings, "tall", maxRows: 4, header: 0.24, new RowStyle(0.34, 0.12, Stacked: true)) },
			],
		};
	}

	internal static IReadOnlyList<TodoItem> Visible(TodoUiState state, TodoWidgetSettings settings) =>
		state.Snapshot.Value.Query(
			settings.Sort,
			settings.OverdueFirst,
			settings.ShowCompleted,
			settings.StatusFilter,
			state.Now.Value,
			state.Thresholds);

	private static UiStack Compact(TodoUiState state) => new UiStack
	{
		Key = "compact",
		Direction = UiComponentDirections.Vertical,
		Justify = UiComponentJustify.Center,
		Align = UiComponentAlignments.Center,
		Padding = UiSize.FromBasis(Inset),
		Gap = UiSize.FromBasis(0.02),
		Children =
		[
			new UiIcon
			{
				Key = "icon",
				Icon = UiValue.From(() => state.Status.Value == TodoStoreStatus.Failed ? UiIcons.AlertTriangle : UiIcons.Check),
				Size = UiSize.FromBasis(0.2),
				Role = UiComponentTextRoles.Secondary,
			},
			new UiTextRun
			{
				Key = "count",
				Text = UiText.From(() => state.Status.Value == TodoStoreStatus.Ready ? Counts(state).Open.ToString(CultureInfo.InvariantCulture) : "-"),
				Size = UiSize.FromBasis(0.3),
				MinSize = UiSize.FromBasis(0.2),
				Weight = UiComponentTextWeights.Bold,
				Role = UiComponentTextRoles.Primary,
				Align = UiComponentAlignments.Center,
				MaxLines = 1,
			},
			new UiTextRun
			{
				Key = "caption",
				Text = UiText.Optional(() => Caption(state)),
				Size = UiSize.FromBasis(0.1),
				MinSize = UiSize.FromBasis(0.07),
				Align = UiComponentAlignments.Center,
				Weight = UiComponentTextWeights.SemiBold,
				Color = UiValue.Optional(() => state.Status.Value == TodoStoreStatus.Ready && Counts(state).Overdue > 0
					? UiValue.Of(TodoLabels.OverdueColor)
					: UiValue.None<string>()),
				Role = UiComponentTextRoles.Muted,
				MaxLines = 1,
			},
		],
	};

	private static UiText Caption(TodoUiState state) => state.Status.Value switch
	{
		TodoStoreStatus.Loading => Strings.Widget.Loading(),
		TodoStoreStatus.Failed => Strings.Widget.Unavailable(),
		_ when Counts(state) is { Overdue: > 0 } counts => Strings.Widget.OverdueCount(counts.Overdue.ToString(CultureInfo.InvariantCulture)),
		_ => Strings.Widget.OpenCaption(),
	};

	private static UiStack Rows(
		TodoUiState state,
		TodoWidgetSettings settings,
		string key,
		int maxRows,
		double header,
		RowStyle style) => new UiStack
	{
		Key = key,
		Direction = UiComponentDirections.Vertical,
		Padding = UiSize.FromBasis(Inset),
		Gap = UiSize.FromBasis(0.03),
		Children =
		[
			Header(state, key, header, pageButton: null),
			Body(state, settings, key, Math.Min(maxRows, settings.MaxVisible), style, commands: null, page: null),
		],
	};

	internal static UiStack Large(TodoUiState state, TodoWidgetSettings settings, ITodoCommands commands, UiState<int> page)
	{
		var rows = settings.MaxVisible;
		if (!settings.Interactive)
		{
			return new UiStack
			{
				Key = "large",
				Direction = UiComponentDirections.Vertical,
				Padding = UiSize.FromBasis(Inset * 0.6),
				Gap = UiSize.FromBasis(0.02),
				Children =
				[
					Header(state, "large", 0.13, pageButton: null),
					Body(state, settings, "large", rows, LargeRow, commands: null, page: null),
				],
			};
		}

		// The page button is the first control in tree order, so a hardware key press pages instead of
		// completing a todo by accident.
		var pageButton = new UiButton
		{
			Key = "large-page",
			MainSize = UiSize.FromBasis(0.2),
			Background = "transparent",
			Direction = UiComponentDirections.Horizontal,
			Justify = UiComponentJustify.End,
			Align = UiComponentAlignments.Center,
			Events = [UiEventHandler.On(UiComponentEvents.Press, () => page.Set(NextPage(page.Peek(), PageCount(state, settings))))],
			// With a single page there is nothing to page to, so the button shows nothing rather than a dead "1/1".
			Children =
			[
				new UiWhen
				{
					Key = "large-page-when",
					Condition = () => PageCount(state, settings) > 1,
					Content = () => new UiStack
					{
						Key = "large-page-content",
						Direction = UiComponentDirections.Horizontal,
						Justify = UiComponentJustify.End,
						Align = UiComponentAlignments.Center,
						Children =
						[
							new UiTextRun
							{
								Key = "large-page-label",
								Text = UiText.From(() => string.Create(CultureInfo.InvariantCulture, $"{CurrentPage(state, settings, page) + 1}/{PageCount(state, settings)}")),
								Size = UiSize.FromBasis(0.055),
								Role = UiComponentTextRoles.Muted,
								MaxLines = 1,
							},
							new UiIcon { Key = "large-page-icon", Icon = UiIcons.ChevronRight, Size = UiSize.FromBasis(0.06), Role = UiComponentTextRoles.Muted },
						],
					},
				},
			],
		};

		return new UiStack
		{
			Key = "large",
			Direction = UiComponentDirections.Vertical,
			Padding = UiSize.FromBasis(Inset * 0.6),
			Gap = UiSize.FromBasis(0.02),
			Children =
			[
				Header(state, "large", 0.13, pageButton),
				Body(state, settings, "large", rows, LargeRow, commands, page),
			],
		};
	}

	private static UiStack Header(TodoUiState state, string key, double height, UiElement? pageButton) => new UiStack
	{
		Key = "header",
		Direction = UiComponentDirections.Horizontal,
		Align = UiComponentAlignments.Center,
		MainSize = UiSize.FromBasis(height),
		Gap = UiSize.FromBasis(0.03),
		Children =
		[
			new UiTextRun
			{
				Key = "count",
				Text = UiText.From(() => state.Status.Value == TodoStoreStatus.Ready ? Counts(state).Open.ToString(CultureInfo.InvariantCulture) : "-"),
				Size = UiSize.FromBasis(height * 0.75),
				Weight = UiComponentTextWeights.Bold,
				Role = UiComponentTextRoles.Primary,
				MaxLines = 1,
			},
			new UiTextRun
			{
				Key = "title",
				Fill = true,
				Text = UiText.Optional(() => Caption(state)),
				Size = UiSize.FromBasis(height * 0.42),
				MinSize = UiSize.FromBasis(height * 0.3),
				Weight = UiComponentTextWeights.SemiBold,
				Color = UiValue.Optional(() => state.Status.Value == TodoStoreStatus.Ready && Counts(state).Overdue > 0
					? UiValue.Of(TodoLabels.OverdueColor)
					: UiValue.None<string>()),
				Role = UiComponentTextRoles.Secondary,
				MaxLines = 1,
			},
			.. pageButton is null ? Array.Empty<UiElement>() : [pageButton],
		],
	};

	private static UiStack Body(
		TodoUiState state,
		TodoWidgetSettings settings,
		string key,
		int rows,
		RowStyle style,
		ITodoCommands? commands,
		UiState<int>? page) => new UiStack
	{
		Key = "body",
		Direction = UiComponentDirections.Vertical,
		Fill = true,
		Gap = UiSize.FromBasis(style.Height * 0.12),
		Children =
		[
			new UiWhen
			{
				Key = "empty-when",
				Condition = () => state.Status.Value == TodoStoreStatus.Ready && Visible(state, settings).Count == 0,
				Content = () => new UiTextRun
				{
					Key = "empty",
					Text = settings.StatusFilter == TodoStatusFilter.All ? Strings.Widget.Empty() : Strings.Widget.EmptyFiltered(),
					Size = UiSize.FromBasis(style.Text),
					Role = UiComponentTextRoles.Muted,
					MaxLines = 2,
					Wrap = true,
				},
			},
			new UiWhen
			{
				Key = "failed-when",
				Condition = () => state.Status.Value == TodoStoreStatus.Failed,
				Content = () => new UiTextRun
				{
					Key = "failed",
					Text = Strings.Errors.StoreUnavailable(),
					Size = UiSize.FromBasis(style.Text * 0.85),
					Role = UiComponentTextRoles.Muted,
					MaxLines = 3,
					Wrap = true,
				},
			},
			new UiRepeat<TodoItem>
			{
				Key = "rows",
				Items = UiValue.From<IReadOnlyList<TodoItem>>(() => PageItems(state, settings, rows, page)),
				KeySelector = todo => todo.Id.ToString("N"),
				Template = (todo, rowKey) => Row(state, settings, todo, rowKey, style, commands),
			},
		],
	};

	private static UiStack Row(
		TodoUiState state,
		TodoWidgetSettings settings,
		TodoItem todo,
		string key,
		RowStyle style,
		ITodoCommands? commands)
	{
		var markerSize = style.Text * 1.1;
		var marker = Marker(state, todo, "marker", markerSize);
		UiElement leading = commands is null
			? marker
			: new UiButton
			{
				Key = "toggle",
				MainSize = UiSize.FromBasis(markerSize * 1.8),
				Background = "transparent",
				Align = UiComponentAlignments.Center,
				Justify = UiComponentJustify.Center,
				Events =
				[
					UiEventHandler.OnAsync(UiComponentEvents.Press, cancellationToken =>
						commands.SetCompletedAsync(todo.Id, !todo.IsCompleted, cancellationToken)),
				],
				Children = [marker],
			};

		var title = new UiTextRun
		{
			Key = "title",
			Fill = !style.Stacked,
			Text = todo.Title,
			Size = UiSize.FromBasis(style.Text),
			Role = todo.IsCompleted ? UiComponentTextRoles.Muted : UiComponentTextRoles.Primary,
			Weight = UiComponentTextWeights.Medium,
			MaxLines = 1,
		};

		var showDue = settings.ShowDueDates && todo.DueAt is not null;
		UiElement[] text = style.Stacked
			?
			[
				new UiStack
				{
					Key = "text",
					Fill = true,
					Direction = UiComponentDirections.Vertical,
					Justify = UiComponentJustify.Center,
					Children = showDue ? [title, DueText(state, todo, style.Text * 0.85, mainSize: null)] : [title],
				},
			]
			: showDue
				? [title, DueText(state, todo, style.Text * 0.85, mainSize: style.Text * 5.6)]
				: [title];

		return new UiStack
		{
			Key = key,
			Direction = UiComponentDirections.Horizontal,
			Align = UiComponentAlignments.Center,
			MainSize = UiSize.FromBasis(style.Height),
			Gap = UiSize.FromBasis(style.Text * 0.45),
			Children = [leading, .. text],
		};
	}

	private static UiTextRun DueText(TodoUiState state, TodoItem todo, double size, double? mainSize) => new UiTextRun
	{
		Key = "due",
		MainSize = mainSize is { } width ? UiSize.FromBasis(width) : UiSize.None(),
		Align = mainSize is null ? UiComponentAlignments.Start : UiComponentAlignments.End,
		Text = UiText.Optional(() => TodoLabels.Due(todo, TodoStatusRules.Evaluate(todo, state.Now.Value, state.Thresholds), state.Now.Value)),
		Size = UiSize.FromBasis(size),
		Color = UiValue.Optional(() => TodoLabels.DueColorFor(TodoStatusRules.Evaluate(todo, state.Now.Value, state.Thresholds)) is { } color
			? UiValue.Of(color)
			: UiValue.None<string>()),
		Role = UiComponentTextRoles.Muted,
		Weight = UiComponentTextWeights.Medium,
		MaxLines = 1,
	};

	internal static UiElement Marker(TodoUiState state, TodoItem todo, string key, double size) => todo.IsCompleted
		? new UiIcon { Key = key, Icon = UiIcons.Check, Size = UiSize.FromBasis(size), Color = TodoLabels.DoneColor }
		: new UiShape
		{
			Key = key,
			Shape = UiComponentShapes.Circle,
			StrokeColor = UiValue.From(() =>
				TodoStatusRules.Evaluate(todo, state.Now.Value, state.Thresholds) == TodoStatus.Overdue
					? TodoLabels.OverdueColor
					: TodoLabels.OpenRingColor),
			StrokeWidth = UiSize.FromBasis(size * 0.12),
			MainSize = UiSize.FromBasis(size),
		};

	private static readonly RowStyle LargeRow = new(0.155, 0.06, Stacked: true);

	internal sealed record RowStyle(double Height, double Text, bool Stacked);

	private static TodoCounts Counts(TodoUiState state) => state.Snapshot.Value.Count(state.Now.Value, state.Thresholds);

	private static int PageCount(TodoUiState state, TodoWidgetSettings settings) =>
		Math.Max(1, (int)Math.Ceiling(Visible(state, settings).Count / (double)settings.MaxVisible));

	private static int CurrentPage(TodoUiState state, TodoWidgetSettings settings, UiState<int> page) =>
		Math.Min(page.Value, PageCount(state, settings) - 1);

	internal static int NextPage(int current, int pageCount) => pageCount <= 1 ? 0 : (current + 1) % pageCount;

	private static IReadOnlyList<TodoItem> PageItems(TodoUiState state, TodoWidgetSettings settings, int rows, UiState<int>? page)
	{
		var visible = Visible(state, settings);
		var skip = page is null ? 0 : CurrentPage(state, settings, page) * settings.MaxVisible;
		return [.. visible.Skip(skip).Take(rows)];
	}
}
