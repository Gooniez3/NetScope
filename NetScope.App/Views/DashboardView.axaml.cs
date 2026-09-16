using Avalonia.Controls;
using Avalonia.Threading;
using NetScope.App.ViewModels;
using ScottPlot;

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
        StyleChart();

        if (DataContext is DashboardViewModel vm)
        {
            vm.ChartUpdated += OnChartUpdated;
            await vm.LoadCommand.ExecuteAsync(null);
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
        Dispatcher.UIThread.Post(RefreshChart);
    }

    private void RefreshChart()
    {
        if (DataContext is not DashboardViewModel vm) return;
        if (vm.ChartTimestamps.Count < 2) return;

        var plt = LatencyChart.Plot;
        plt.Clear();

        var scatter = plt.Add.Scatter(
            vm.ChartTimestamps.ToArray(),
            vm.ChartLatencies.ToArray());
        scatter.LineWidth = 1.5f;
        scatter.MarkerSize = 3;
        scatter.Color = ScottPlot.Color.FromHex("#58A6FF");
        scatter.MarkerColor = ScottPlot.Color.FromHex("#58A6FF");

        plt.Axes.DateTimeTicksBottom();
        plt.Axes.AutoScale();
        LatencyChart.Refresh();
    }

    private void StyleChart()
    {
        var plt = LatencyChart.Plot;
        plt.FigureBackground.Color = ScottPlot.Color.FromHex("#161B22");
        plt.DataBackground.Color = ScottPlot.Color.FromHex("#0D1117");
        plt.Grid.MajorLineColor = ScottPlot.Color.FromHex("#21262D");
        plt.Axes.Color(ScottPlot.Color.FromHex("#484F58"));
        plt.Axes.Bottom.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#8B949E");
        plt.Axes.Left.TickLabelStyle.ForeColor = ScottPlot.Color.FromHex("#8B949E");
        plt.Axes.Left.Label.Text = "ms";
        plt.Axes.Left.Label.ForeColor = ScottPlot.Color.FromHex("#484F58");
        plt.Legend.IsVisible = false;
        LatencyChart.Refresh();
    }
}
