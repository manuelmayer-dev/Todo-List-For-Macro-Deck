namespace TodoList.Core.Storage;

/// <summary>
/// One schema step. Migrations are additive only, so a database that a newer plugin version already
/// migrated stays readable after a rollback.
/// </summary>
internal sealed record Migration(int Version, string Sql);

internal static class Migrations
{
	public static IReadOnlyList<Migration> All { get; } =
	[
		new(1, """
			CREATE TABLE todos (
				id TEXT NOT NULL PRIMARY KEY,
				title TEXT NOT NULL,
				description TEXT NULL,
				created_at INTEGER NOT NULL,
				due_at INTEGER NULL,
				completed_at INTEGER NULL,
				sort_order INTEGER NOT NULL,
				due_notified_at INTEGER NULL,
				overdue_notified_at INTEGER NULL
			);
			CREATE INDEX ix_todos_open_due ON todos (completed_at, due_at);
			CREATE TABLE settings (
				key TEXT NOT NULL PRIMARY KEY,
				value TEXT NOT NULL
			);
			"""),
	];
}
