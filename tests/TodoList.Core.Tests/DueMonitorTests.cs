using MacroDeck.Plugin.Testing;
using NUnit.Framework;
using TodoList.Core.Scheduling;

namespace TodoList.Core.Tests;

[TestFixture]
public sealed class DueMonitorTests
{
	private static async Task<(TestStore Store, DueMonitor Monitor, List<(DueTransitionKind Kind, string Title)> Raised)> StartAsync(TestStore? existing = null)
	{
		var store = existing ?? await TestStore.CreateAsync();
		var monitor = new DueMonitor(store.Todos, store.Settings, store.Clock, TestStore.Logger);
		var raised = new List<(DueTransitionKind, string)>();
		monitor.TransitionOccurred += (_, t) =>
		{
			lock (raised)
			{
				raised.Add((t.Kind, t.Todo.Title));
			}
		};
		await monitor.StartAsync(CancellationToken.None);
		return (store, monitor, raised);
	}

	private static Task Settled(DueMonitor monitor, TestStore store) =>
		Wait.UntilAsync(
			() => monitor.NextCheckAt is null || monitor.NextCheckAt > store.Clock.GetUtcNow(),
			TimeSpan.FromSeconds(5),
			"the monitor re-armed its timer");

	private static Task Armed(DueMonitor monitor, TestStore store) =>
		Wait.UntilAsync(
			() => monitor.NextCheckAt > store.Clock.GetUtcNow(),
			TimeSpan.FromSeconds(5),
			"the monitor armed its timer for a future transition");

	[Test]
	public async Task A_todo_becomes_due_then_overdue_once_each()
	{
		var (store, monitor, raised) = await StartAsync();
		await using var _ = store;
		using var __ = monitor;
		await store.Todos.CreateAsync(new TodoDraft("Submit", DueAt: TestStore.Start.AddMinutes(90)), CancellationToken.None);
		await Armed(monitor, store);

		store.Clock.Advance(TimeSpan.FromMinutes(30));
		await Wait.UntilAsync(() => raised.Count == 1, TimeSpan.FromSeconds(5));
		await Armed(monitor, store);
		store.Clock.Advance(TimeSpan.FromMinutes(60));
		await Wait.UntilAsync(() => raised.Count == 2, TimeSpan.FromSeconds(5));
		await Settled(monitor, store);
		store.Clock.Advance(TimeSpan.FromHours(3));
		await monitor.ReconcileAsync(CancellationToken.None);

		Assert.That(raised, Is.EqualTo(new[] { (DueTransitionKind.Due, "Submit"), (DueTransitionKind.Overdue, "Submit") }));
	}

	[Test]
	public async Task The_timer_is_armed_for_the_next_deadline_not_polled()
	{
		var (store, monitor, _) = await StartAsync();
		await using var _ = store;
		using var __ = monitor;

		await store.Todos.CreateAsync(new TodoDraft("Soon", DueAt: TestStore.Start.AddMinutes(63)), CancellationToken.None);
		await Wait.UntilAsync(() => monitor.NextCheckAt is not null, TimeSpan.FromSeconds(5));

		Assert.That(monitor.NextCheckAt, Is.EqualTo(TestStore.Start.AddMinutes(3)));
	}

	[Test]
	public async Task Nothing_is_armed_when_no_todo_has_a_due_date()
	{
		var (store, monitor, _) = await StartAsync();
		await using var _ = store;
		using var __ = monitor;

		await store.Todos.CreateAsync(new TodoDraft("Someday"), CancellationToken.None);
		await monitor.ReconcileAsync(CancellationToken.None);

		Assert.That(monitor.NextCheckAt, Is.Null);
	}

	[Test]
	public async Task Long_waits_are_capped_so_sleep_and_clock_changes_are_noticed()
	{
		var (store, monitor, _) = await StartAsync();
		await using var _ = store;
		using var __ = monitor;

		await store.Todos.CreateAsync(new TodoDraft("Next month", DueAt: TestStore.Start.AddDays(30)), CancellationToken.None);
		await Wait.UntilAsync(() => monitor.NextCheckAt is not null, TimeSpan.FromSeconds(5));

		Assert.That(monitor.NextCheckAt, Is.EqualTo(TestStore.Start + DueMonitor.MaxTimerDelay));
	}

	[Test]
	public async Task A_todo_already_past_its_deadline_raises_only_overdue()
	{
		var store = await TestStore.CreateAsync();
		await store.Todos.CreateAsync(new TodoDraft("Missed", DueAt: TestStore.Start.AddHours(-3)), CancellationToken.None);

		var (_, monitor, raised) = await StartAsync(store);
		await using var _ = store;
		using var __ = monitor;

		Assert.That(raised, Is.EqualTo(new[] { (DueTransitionKind.Overdue, "Missed") }));
	}

	[Test]
	public async Task A_restart_does_not_raise_transitions_again()
	{
		var store = await TestStore.CreateAsync();
		await using var _ = store;
		await store.Todos.CreateAsync(new TodoDraft("Once", DueAt: TestStore.Start.AddMinutes(-5)), CancellationToken.None);
		var (_, first, firstRaised) = await StartAsync(store);
		first.Dispose();

		await store.ReopenAsync();
		var (_, second, secondRaised) = await StartAsync(store);
		using var __ = second;

		Assert.That(firstRaised, Has.Count.EqualTo(1));
		Assert.That(secondRaised, Is.Empty);
	}

	[Test]
	public async Task Transitions_missed_while_stopped_are_raised_on_start()
	{
		var store = await TestStore.CreateAsync();
		await using var _ = store;
		await store.Todos.CreateAsync(new TodoDraft("While offline", DueAt: TestStore.Start.AddMinutes(30)), CancellationToken.None);

		store.Clock.Advance(TimeSpan.FromHours(2));
		var (_, monitor, raised) = await StartAsync(store);
		using var __ = monitor;

		Assert.That(raised, Is.EqualTo(new[] { (DueTransitionKind.Overdue, "While offline") }));
	}

	[Test]
	public async Task Completed_and_deleted_todos_never_raise()
	{
		var (store, monitor, raised) = await StartAsync();
		await using var _ = store;
		using var __ = monitor;
		var done = await store.Todos.CreateAsync(new TodoDraft("Done", DueAt: TestStore.Start.AddMinutes(10)), CancellationToken.None);
		var gone = await store.Todos.CreateAsync(new TodoDraft("Gone", DueAt: TestStore.Start.AddMinutes(10)), CancellationToken.None);
		await Settled(monitor, store);
		raised.Clear();

		await store.Todos.CompleteAsync(done.Id, CancellationToken.None);
		await store.Todos.DeleteAsync(gone.Id, CancellationToken.None);
		await Settled(monitor, store);
		store.Clock.Advance(TimeSpan.FromHours(1));
		await monitor.ReconcileAsync(CancellationToken.None);

		Assert.That(raised.Where(r => r.Kind == DueTransitionKind.Overdue), Is.Empty);
		Assert.That(monitor.NextCheckAt, Is.Null);
	}

	[Test]
	public async Task Reopening_an_overdue_todo_raises_overdue_again()
	{
		var (store, monitor, raised) = await StartAsync();
		await using var _ = store;
		using var __ = monitor;
		var todo = await store.Todos.CreateAsync(new TodoDraft("Again", DueAt: TestStore.Start.AddMinutes(-1)), CancellationToken.None);
		await Wait.UntilAsync(() => raised.Count == 1, TimeSpan.FromSeconds(5));

		await store.Todos.CompleteAsync(todo.Id, CancellationToken.None);
		await store.Todos.ReopenAsync(todo.Id, CancellationToken.None);
		await Wait.UntilAsync(() => raised.Count == 2, TimeSpan.FromSeconds(5));

		Assert.That(raised.Select(r => r.Kind), Is.EqualTo(new[] { DueTransitionKind.Overdue, DueTransitionKind.Overdue }));
	}

	[Test]
	public async Task Moving_the_deadline_starts_a_new_cycle()
	{
		var (store, monitor, raised) = await StartAsync();
		await using var _ = store;
		using var __ = monitor;
		var todo = await store.Todos.CreateAsync(new TodoDraft("Shifted", DueAt: TestStore.Start.AddMinutes(-1)), CancellationToken.None);
		await Wait.UntilAsync(() => raised.Count == 1, TimeSpan.FromSeconds(5));

		await store.Todos.UpdateAsync(todo.Id, new TodoDraft("Shifted", DueAt: TestStore.Start.AddMinutes(30)), CancellationToken.None);
		await Wait.UntilAsync(() => raised.Count == 2, TimeSpan.FromSeconds(5));

		Assert.That(raised[1], Is.EqualTo((DueTransitionKind.Due, "Shifted")));
	}

	[Test]
	public async Task A_closed_delivery_gate_holds_transitions_without_recording_them()
	{
		var store = await TestStore.CreateAsync();
		await using var _ = store;
		var gate = new Gate { IsOpen = false };
		using var monitor = new DueMonitor(store.Todos, store.Settings, store.Clock, TestStore.Logger, gate);
		var raised = new List<DueTransitionKind>();
		monitor.TransitionOccurred += (_, t) => raised.Add(t.Kind);
		var todo = await store.Todos.CreateAsync(new TodoDraft("Held", DueAt: TestStore.Start.AddMinutes(-1)), CancellationToken.None);

		await monitor.StartAsync(CancellationToken.None);
		var markedWhileClosed = store.Todos.Snapshot.Find(todo.Id)!.OverdueNotifiedAt;
		gate.IsOpen = true;
		await monitor.ReconcileAsync(CancellationToken.None);

		Assert.That(markedWhileClosed, Is.Null);
		Assert.That(raised, Is.EqualTo(new[] { DueTransitionKind.Overdue }));
	}

	private sealed class Gate : ITransitionDeliveryGate
	{
		public bool IsOpen { get; set; }
	}

	[Test]
	public async Task Stopping_cancels_the_pending_timer()
	{
		var (store, monitor, raised) = await StartAsync();
		await using var _ = store;
		await store.Todos.CreateAsync(new TodoDraft("Later", DueAt: TestStore.Start.AddMinutes(90)), CancellationToken.None);
		await Armed(monitor, store);

		monitor.Stop();
		store.Clock.Advance(TimeSpan.FromHours(3));

		Assert.That(monitor.NextCheckAt, Is.Null);
		Assert.That(raised, Is.Empty);
		monitor.Dispose();
	}
}
