using System.Windows;
using ClonerApp.App.ViewModels;

namespace ClonerApp.App.Views;

public partial class WizardWindow : Window
{
    public WizardWindow(WizardViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.CloseRequested += (_, _) =>
        {
            DialogResult = true;
            Close();
        };
    }
}
