using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NetScope.App.ViewModels;
using NetScope.App.Views;

namespace NetScope.App;

public partial class App : Application
{
    internal static bool IsExiting { get; private set; }

    private MainViewModel? _main;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        AppDefaults.Load();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _main = new MainViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = _main
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                IsExiting = true;
                StopMonitors();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    public void ShowMainWindow()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return;

        var window = desktop.MainWindow;
        if (window is null)
            return;

        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }

    public void ExitApplication()
    {
        IsExiting = true;
        StopMonitors();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.Shutdown();
    }

    private void StopMonitors()
    {
        if (_main is null)
            return;
        if (_main.Monitor.IsMonitoring)
            _main.Monitor.StopCommand.Execute(null);
        _main.Dashboard.StopThroughput();
    }

    private void OnTrayClicked(object? sender, EventArgs e) => ShowMainWindow();

    private void OnTrayShow(object? sender, EventArgs e) => ShowMainWindow();

    private void OnTrayDashboard(object? sender, EventArgs e)
    {
        _main?.NavigateCommand.Execute("Dashboard");
        ShowMainWindow();
    }

    private void OnTrayExit(object? sender, EventArgs e) => ExitApplication();
}
