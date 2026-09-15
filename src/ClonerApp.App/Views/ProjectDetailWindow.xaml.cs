using System.Windows;
using ClonerApp.App.ViewModels;

namespace ClonerApp.App.Views;

public partial class ProjectDetailWindow : Window
{
    public ProjectDetailViewModel ViewModel { get; }

    public ProjectDetailWindow(ProjectDetailViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
