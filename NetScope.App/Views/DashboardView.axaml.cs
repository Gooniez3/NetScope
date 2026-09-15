using Avalonia.Controls;
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
        if (DataContext is DashboardViewModel vm)
        {
            await vm.LoadCommand.ExecuteAsync(null);
        }
    }
}
