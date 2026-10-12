using NUnit.Framework;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class TodoOrderingTests
{
	private static readonly DateTimeOffset Now = new(2026, 5, 1, 12, 0, 0, TimeSpan.Zero);

	private static TodoItem Todo(string title, int order, DateTimeOffset? due = null, int createdHoursAgo = 0, DateTimeOffset? completed = null) => new()
	{
		Id = Guid.NewGuid(),
		Title = title,
		CreatedAt = Now.AddHours(-createdHoursAgo),
		DueAt = due,
		CompletedAt = completed,
		SortOrder = order,
	};

	private static string[] Titles(IEnumerable<TodoItem> items) => [.. items.Select(t => t.Title)];

	[Test]
	public void Due_date_order_puts_todos_without_due_date_last()
	{
		var items = new[]
		{
			Todo("none", 0),
			Todo("later", 1, Now.AddDays(2)),
			Todo("sooner", 2, Now.AddHours(3)),
		};

		var sorted = TodoOrdering.Sort(items, TodoSortOrder.DueDate, overdueFirst: false, Now, TodoThresholds.Default);

		Assert.That(Titles(sorted), Is.EqualTo(new[] { "sooner", "later", "none" }));
	}

	[Test]
	public void Created_order_lists_newest_first()
	{
		var items = new[] { Todo("old", 0, createdHoursAgo: 5), Todo("new", 1, createdHoursAgo: 1) };

		var sorted = TodoOrdering.Sort(items, TodoSortOrder.CreatedDate, overdueFirst: false, Now, TodoThresholds.Default);

		Assert.That(Titles(sorted), Is.EqualTo(new[] { "new", "old" }));
	}

	[Test]
	public void Overdue_first_lifts_overdue_todos_above_the_chosen_order()
	{
		var items = new[]
		{
			Todo("first", 0),
			Todo("overdue", 1, Now.AddHours(-1)),
		};

		var sorted = TodoOrdering.Sort(items, TodoSortOrder.Manual, overdueFirst: true, Now, TodoThresholds.Default);

		Assert.That(Titles(sorted), Is.EqualTo(new[] { "overdue", "first" }));
	}

	[Test]
	public void Completed_todos_follow_open_ones_most_recent_first()
	{
		var items = new[]
		{
			Todo("done long ago", 0, completed: Now.AddDays(-3)),
			Todo("open", 1),
			Todo("done just now", 2, completed: Now.AddMinutes(-1)),
		};

		var sorted = TodoOrdering.Sort(items, TodoSortOrder.Manual, overdueFirst: false, Now, TodoThresholds.Default);

		Assert.That(Titles(sorted), Is.EqualTo(new[] { "open", "done just now", "done long ago" }));
	}

	[Test]
	public void A_status_filter_excludes_completed_todos()
	{
		var snapshot = new TodoSnapshot(
		[
			Todo("overdue", 0, Now.AddHours(-1)),
			Todo("due", 1, Now.AddMinutes(30)),
			Todo("open", 2),
			Todo("done", 3, Now.AddHours(-2), completed: Now),
		]);

		var dueOrOverdue = snapshot.Query(TodoSortOrder.Manual, false, includeCompleted: true, TodoStatusFilter.DueOrOverdue, Now, TodoThresholds.Default);

		Assert.That(Titles(dueOrOverdue), Is.EqualTo(new[] { "overdue", "due" }));
	}

	[Test]
	public void Counts_treat_due_and_overdue_todos_as_open()
	{
		var snapshot = new TodoSnapshot(
		[
			Todo("overdue", 0, Now.AddHours(-1)),
			Todo("due", 1, Now.AddMinutes(30)),
			Todo("open", 2),
			Todo("done", 3, completed: Now),
		]);

		Assert.That(snapshot.Count(Now, TodoThresholds.Default), Is.EqualTo(new TodoCounts(Open: 3, Completed: 1, Due: 1, Overdue: 1, Total: 4)));
	}

	[Test]
	public void Next_due_includes_overdue_todos()
	{
		var snapshot = new TodoSnapshot(
		[
			Todo("tomorrow", 0, Now.AddDays(1)),
			Todo("yesterday", 1, Now.AddDays(-1)),
			Todo("no date", 2),
		]);

		Assert.That(snapshot.NextDue()?.Title, Is.EqualTo("yesterday"));
	}
}
