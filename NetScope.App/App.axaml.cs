using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using NetScope.App.ViewModels;
using NetScope.App.Views;

namespace NetScope.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            desktop.MainWindow = new MainWindow
            {
                DataContext = vm,
            };

            desktop.ShutdownRequested += (_, _) =>
            {
                if (vm.Dashboard.IsMonitoring)
                    vm.Dashboard.StopMonitorCommand.Execute(null);
                if (vm.Monitor.IsMonitoring)
                    vm.Monitor.StopCommand.Execute(null);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
