using MacroDeck.Sdk.Ui;
using MacroDeck.Ui.Model.Events;
using MacroDeck.Ui.Model.Nodes;
using MacroDeck.Ui.Model.Patches;
using MacroDeck.Ui.Runtime;

namespace TodoList.Plugin.Ui;

/// <summary>
/// Adapts a reactive <see cref="UiView"/> to a host session and releases what the view holds when the host
/// closes it.
/// </summary>
internal sealed class ViewSession : IUiSession
{
	private readonly UiView _view;
	private readonly IDisposable? _lease;

	public ViewSession(UiView view, IDisposable? lease)
	{
		_view = view;
		_lease = lease;
		_view.Changed += OnChanged;
		_view.HandlerFaulted += OnHandlerFaulted;
	}

	public event EventHandler? Changed;

	public event EventHandler<UiSessionFaultedEventArgs>? Faulted;

	public UiTree BuildTree() => _view.Tree;

	public IReadOnlyList<UiPatch> DrainPatches() => _view.DrainPatches();

	public void Dispatch(UiEvent uiEvent) => _view.Dispatch(uiEvent);

	public ValueTask DisposeAsync()
	{
		_view.Changed -= OnChanged;
		_view.HandlerFaulted -= OnHandlerFaulted;
		_view.Dispose();
		_lease?.Dispose();
		return ValueTask.CompletedTask;
	}

	private void OnChanged(object? sender, EventArgs e) => Changed?.Invoke(this, EventArgs.Empty);

	private void OnHandlerFaulted(object? sender, UiHandlerFaultEventArgs e) =>
		Faulted?.Invoke(this, new UiSessionFaultedEventArgs(e.Exception.Message, e.Exception));
}
