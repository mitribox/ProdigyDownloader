using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ClonerApp.App.ViewModels;

namespace ClonerApp.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void ProjectsGrid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var row = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject);
        if (row?.Item is null) return;
        row.IsSelected = true;
        row.Focus();
        ProjectsGrid.SelectedItem = row.Item;
    }

    private void ProjectsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (ProjectsGrid.ContextMenu is not null)
            ProjectsGrid.ContextMenu.DataContext = DataContext;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
