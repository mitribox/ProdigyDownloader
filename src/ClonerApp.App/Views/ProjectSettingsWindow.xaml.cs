using System.Windows;
using ClonerApp.App.ViewModels;

namespace ClonerApp.App.Views;

public partial class ProjectSettingsWindow : Window
{
    public ProjectSettingsViewModel ViewModel { get; }

    public ProjectSettingsWindow(ProjectSettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) =>
        {
            try { DialogResult = true; } catch { /* non-dialog */ }
            Close();
        };
    }
}
