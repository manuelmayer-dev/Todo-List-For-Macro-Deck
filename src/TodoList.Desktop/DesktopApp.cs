using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace TodoList.Desktop;

internal sealed class DesktopApp(Action<DesktopApp, IClassicDesktopStyleApplicationLifetime> started) : Application
{
	public override void Initialize()
	{
		Name = Localization.DesktopText.AppName;
		Theme.AppTheme.Apply(this);
	}

	public override void OnFrameworkInitializationCompleted()
	{
		if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
		{
			started(this, lifetime);
		}

		base.OnFrameworkInitializationCompleted();
	}
}
