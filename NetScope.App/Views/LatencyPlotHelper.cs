using ScottPlot;
using ScottPlot.Avalonia;

namespace NetScope.App.Views;

/// <summary>
/// Shared ScottPlot styling for live latency graphs. Presentation only.
/// </summary>
internal static class LatencyPlotHelper
{
    public static void Apply(AvaPlot chart)
    {
        var plt = chart.Plot;
        plt.FigureBackground.Color = Color.FromHex("#0D1117");
        plt.DataBackground.Color = Color.FromHex("#0D1117");
        plt.Grid.MajorLineColor = Color.FromHex("#21262D");
        plt.Grid.MajorLineWidth = 0.4f;
        plt.Grid.XAxisStyle.MajorLineStyle.IsVisible = false;
        plt.Axes.Color(Color.FromHex("#30363D"));
        plt.Axes.Bottom.TickLabelStyle.ForeColor = Color.FromHex("#6E7681");
        plt.Axes.Bottom.TickLabelStyle.FontSize = 10;
        plt.Axes.Left.TickLabelStyle.ForeColor = Color.FromHex("#6E7681");
        plt.Axes.Left.TickLabelStyle.FontSize = 10;
        plt.Axes.Left.Label.Text = "ms";
        plt.Axes.Left.Label.ForeColor = Color.FromHex("#6E7681");
        plt.Axes.Left.Label.FontSize = 10;
        plt.Axes.Right.IsVisible = false;
        plt.Axes.Top.IsVisible = false;
        plt.Legend.IsVisible = false;
        plt.Axes.Margins(0.08, 0.18);
        plt.Axes.SetLimitsY(0, 25);
        plt.Axes.SetLimitsX(0, 1);
        plt.Axes.Bottom.TickLabelStyle.IsVisible = false;
        chart.Refresh();
    }

    public static void Render(AvaPlot chart, IReadOnlyList<double> timestamps, IReadOnlyList<double> latencies)
    {
        if (timestamps.Count < 2)
            return;

        var plt = chart.Plot;
        plt.Clear();

        var scatter = plt.Add.Scatter(timestamps.ToArray(), latencies.ToArray());
        scatter.LineWidth = 1.2f;
        scatter.MarkerSize = 2;
        scatter.Color = Color.FromHex("#58A6FF");
        scatter.MarkerColor = Color.FromHex("#58A6FF");

        plt.Axes.Bottom.TickLabelStyle.IsVisible = true;
        plt.Axes.DateTimeTicksBottom();

        var tickGen = plt.Axes.Bottom.TickGenerator as ScottPlot.TickGenerators.DateTimeAutomatic;
        if (tickGen is not null)
        {
            tickGen.LabelFormatter = dt => dt.ToString("HH:mm:ss");
        }

        plt.Axes.AutoScale();
        plt.Axes.Margins(0.08, 0.18);
        var limits = plt.Axes.GetLimits();
        var rightPad = Math.Max((limits.Right - limits.Left) * 0.04, 12.0 / 86_400.0);
        plt.Axes.SetLimitsX(limits.Left, limits.Right + rightPad);

        plt.FigureBackground.Color = Color.FromHex("#0D1117");
        plt.DataBackground.Color = Color.FromHex("#0D1117");
        plt.Grid.MajorLineColor = Color.FromHex("#21262D");
        plt.Grid.MajorLineWidth = 0.4f;
        plt.Grid.XAxisStyle.MajorLineStyle.IsVisible = false;
        plt.Axes.Color(Color.FromHex("#30363D"));
        plt.Axes.Bottom.TickLabelStyle.ForeColor = Color.FromHex("#6E7681");
        plt.Axes.Bottom.TickLabelStyle.FontSize = 10;
        plt.Axes.Left.TickLabelStyle.ForeColor = Color.FromHex("#6E7681");
        plt.Axes.Left.TickLabelStyle.FontSize = 10;
        plt.Axes.Left.Label.Text = "ms";
        plt.Axes.Left.Label.ForeColor = Color.FromHex("#6E7681");
        plt.Axes.Left.Label.FontSize = 10;
        plt.Axes.Right.IsVisible = false;
        plt.Axes.Top.IsVisible = false;
        plt.Legend.IsVisible = false;

        chart.Refresh();
    }

    public static void Clear(AvaPlot chart)
    {
        chart.Plot.Clear();
        Apply(chart);
    }
}
