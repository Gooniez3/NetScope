using Avalonia.Controls;
using Avalonia.Threading;
using NetScope.App.ViewModels;

namespace NetScope.App.Views;

public partial class MonitorView : UserControl
{
    public MonitorView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LatencyPlotHelper.Apply(LatencyChart);

        if (DataContext is MonitorViewModel vm)
        {
            vm.ChartUpdated += OnChartUpdated;
            vm.ChartCleared += OnChartCleared;
            LatencyPlotHelper.Render(LatencyChart, vm.ChartTimestamps, vm.ChartLatencies);
        }
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (DataContext is MonitorViewModel vm)
        {
            vm.ChartUpdated -= OnChartUpdated;
            vm.ChartCleared -= OnChartCleared;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void OnChartUpdated()
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (DataContext is MonitorViewModel vm)
                LatencyPlotHelper.Render(LatencyChart, vm.ChartTimestamps, vm.ChartLatencies);
        });
    }

    private void OnChartCleared()
    {
        Dispatcher.UIThread.Post(() => LatencyPlotHelper.Clear(LatencyChart));
    }
}
