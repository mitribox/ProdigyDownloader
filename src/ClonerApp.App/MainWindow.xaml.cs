using System.Windows;
using ClonerApp.App.ViewModels;

namespace ClonerApp.App;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
