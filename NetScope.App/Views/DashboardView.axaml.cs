using Avalonia.Controls;
using Avalonia.Threading;
using NetScope.App.ViewModels;

namespace NetScope.App.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    protected override async void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LatencyPlotHelper.Apply(LatencyChart);

        if (DataContext is DashboardViewModel vm)
        {
            vm.ChartUpdated += OnChartUpdated;
            await vm.LoadCommand.ExecuteAsync(null);
            LatencyPlotHelper.Render(LatencyChart, vm.ChartTimestamps, vm.ChartLatencies);
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.ChartUpdated -= OnChartUpdated;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnChartUpdated()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is DashboardViewModel vm)
                LatencyPlotHelper.Render(LatencyChart, vm.ChartTimestamps, vm.ChartLatencies);
        });
    }
}
