using System.Windows;
using ClonerApp.Core.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ClonerApp.App.ViewModels;

public partial class ProjectSettingsViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private Guid _projectId;

    public ProjectConfigViewModel Config { get; } = new();

    public event EventHandler? CloseRequested;

    [ObservableProperty] private string _windowTitle = "ProdigyDownloader — Project Settings";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    public ProjectSettingsViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task LoadAsync(Guid projectId)
    {
        _projectId = projectId;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = await repo.GetByIdAsync(projectId);
        if (project is null)
        {
            StatusMessage = "Project not found.";
            return;
        }

        Config.LoadFrom(project);
        WindowTitle = $"ProdigyDownloader — {project.Name}";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        Config.ValidationError = Config.Validate();
        if (Config.ValidationError is not null)
        {
            StatusMessage = Config.ValidationError;
            return;
        }

        IsBusy = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            var project = await repo.GetByIdAsync(_projectId);
            if (project is null)
            {
                StatusMessage = "Project not found.";
                return;
            }

            Config.ApplyTo(project);
            await repo.UpdateAsync(project);
            StatusMessage = "Saved.";
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            MessageBox.Show(ex.Message, "Save failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);
}
