using NUnit.Framework;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class TodoStatusRulesTests
{
	private static readonly DateTimeOffset Deadline = new(2026, 5, 1, 15, 0, 0, TimeSpan.Zero);
	private static readonly TodoThresholds OneHourWindow = new(TimeSpan.FromHours(1), TimeSpan.Zero);

	private static TodoItem Todo(DateTimeOffset? due = null, DateTimeOffset? completed = null) => new()
	{
		Id = Guid.NewGuid(),
		Title = "Pay invoice",
		CreatedAt = Deadline.AddDays(-2),
		DueAt = due,
		CompletedAt = completed,
	};

	[Test]
	public void A_todo_without_due_date_is_open() =>
		Assert.That(TodoStatusRules.Evaluate(Todo(), Deadline, OneHourWindow), Is.EqualTo(TodoStatus.Open));

	[Test]
	public void A_todo_before_the_due_window_is_open() =>
		Assert.That(TodoStatusRules.Evaluate(Todo(Deadline), Deadline.AddHours(-1).AddTicks(-1), OneHourWindow), Is.EqualTo(TodoStatus.Open));

	[Test]
	public void A_todo_becomes_due_when_the_window_starts() =>
		Assert.That(TodoStatusRules.Evaluate(Todo(Deadline), Deadline.AddHours(-1), OneHourWindow), Is.EqualTo(TodoStatus.Due));

	[Test]
	public void A_todo_becomes_overdue_exactly_at_the_deadline() =>
		Assert.That(TodoStatusRules.Evaluate(Todo(Deadline), Deadline, OneHourWindow), Is.EqualTo(TodoStatus.Overdue));

	[Test]
	public void A_grace_period_keeps_a_passed_deadline_due_until_it_ends()
	{
		var thresholds = new TodoThresholds(TimeSpan.Zero, TimeSpan.FromMinutes(30));

		Assert.Multiple(() =>
		{
			Assert.That(TodoStatusRules.Evaluate(Todo(Deadline), Deadline.AddMinutes(-1), thresholds), Is.EqualTo(TodoStatus.Open));
			Assert.That(TodoStatusRules.Evaluate(Todo(Deadline), Deadline.AddMinutes(29), thresholds), Is.EqualTo(TodoStatus.Due));
			Assert.That(TodoStatusRules.Evaluate(Todo(Deadline), Deadline.AddMinutes(30), thresholds), Is.EqualTo(TodoStatus.Overdue));
		});
	}

	[Test]
	public void With_both_thresholds_zero_a_todo_is_never_due()
	{
		var thresholds = new TodoThresholds(TimeSpan.Zero, TimeSpan.Zero);

		Assert.Multiple(() =>
		{
			Assert.That(TodoStatusRules.Evaluate(Todo(Deadline), Deadline.AddTicks(-1), thresholds), Is.EqualTo(TodoStatus.Open));
			Assert.That(TodoStatusRules.Evaluate(Todo(Deadline), Deadline, thresholds), Is.EqualTo(TodoStatus.Overdue));
		});
	}

	[Test]
	public void A_completed_todo_is_never_due_or_overdue() =>
		Assert.That(
			TodoStatusRules.Evaluate(Todo(Deadline, completed: Deadline.AddDays(-1)), Deadline.AddDays(5), OneHourWindow),
			Is.EqualTo(TodoStatus.Completed));
}
