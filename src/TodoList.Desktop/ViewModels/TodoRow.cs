using TodoList.Core;

namespace TodoList.Desktop.ViewModels;

internal sealed record TodoRow(TodoItem Todo, TodoStatus Status, string DueText)
{
	public Guid Id => Todo.Id;
}
