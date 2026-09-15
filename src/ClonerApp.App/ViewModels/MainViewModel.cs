using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ClonerApp.App.Views;
using ClonerApp.Core.Enums;
using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using ClonerApp.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ClonerApp.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICrawlEngine _engine;

    public ObservableCollection<ProjectListItem> Projects { get; } = new();

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private ProjectListItem? _selectedProject;

    public MainViewModel(IServiceScopeFactory scopeFactory, ICrawlEngine engine)
    {
        _scopeFactory = scopeFactory;
        _engine = engine;
        _engine.ProgressChanged += OnProgressChanged;
        _ = LoadProjectsAsync();
    }

    private void OnProgressChanged(object? sender, RunProgress e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusText = e.Message;
            var item = Projects.FirstOrDefault(p => p.Id == e.ProjectId);
            if (item is null) return;
            item.LastStatus = e.IsFailed ? "Failed" : e.IsCompleted ? "Completed" : "Running";
            item.Stats = $"↓ {e.Downloaded}  skip {e.Skipped}  filter {e.Filtered}  fail {e.Failed}";
            if (e.IsCompleted || e.IsFailed)
                _ = LoadProjectsAsync();
        });
    }

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var projects = await repo.GetAllAsync();

        Projects.Clear();
        foreach (var p in projects)
        {
            var latest = await runRepo.GetLatestForProjectAsync(p.Id);
            Projects.Add(new ProjectListItem
            {
                Id = p.Id,
                Name = p.Name,
                StartUrl = p.StartUrls.Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "",
                RunMode = p.RunMode.ToString(),
                OutputRoot = p.OutputRoot,
                IsEnabled = p.IsEnabled,
                NextRun = p.NextRunAtUtc?.ToLocalTime().ToString("g") ?? "—",
                LastStatus = latest?.Status.ToString() ?? "Never run",
                Stats = latest is null ? "" : $"↓ {latest.Downloaded}  skip {latest.Skipped}"
            });
        }
    }

    [RelayCommand]
    private void NewProject()
    {
        var wizard = App.GetService<WizardWindow>();
        wizard.Owner = Application.Current.MainWindow;
        if (wizard.ShowDialog() == true)
            _ = LoadProjectsAsync();
    }

    [RelayCommand]
    private async Task EditSelectedAsync()
    {
        if (SelectedProject is null) return;
        if (_engine.IsRunning(SelectedProject.Id))
        {
            StatusText = "Stop the running project before editing.";
            return;
        }

        var window = App.GetService<ProjectSettingsWindow>();
        window.Owner = Application.Current.MainWindow;
        await window.ViewModel.LoadAsync(SelectedProject.Id);
        if (window.ShowDialog() == true)
            await LoadProjectsAsync();
    }

    [RelayCommand]
    private async Task StartSelectedAsync()
    {
        if (SelectedProject is null) return;
        if (_engine.IsRunning(SelectedProject.Id))
        {
            StatusText = "Project is already running.";
            return;
        }

        StatusText = $"Starting {SelectedProject.Name}…";
        SelectedProject.LastStatus = "Running";
        var id = SelectedProject.Id;
        _ = Task.Run(async () =>
        {
            try
            {
                await _engine.RunProjectAsync(id);
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => StatusText = ex.Message);
            }
        });
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void CancelSelected()
    {
        if (SelectedProject is null) return;
        _engine.Cancel(SelectedProject.Id);
        StatusText = "Cancel requested…";
    }

    [RelayCommand]
    private async Task DeleteSelectedAsync()
    {
        if (SelectedProject is null) return;
        var result = MessageBox.Show(
            $"Delete project '{SelectedProject.Name}'? Downloaded files on disk are kept.",
            "Delete Project",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        await repo.DeleteAsync(SelectedProject.Id);
        await LoadProjectsAsync();
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (SelectedProject is null || string.IsNullOrWhiteSpace(SelectedProject.OutputRoot)) return;
        if (!Directory.Exists(SelectedProject.OutputRoot))
            Directory.CreateDirectory(SelectedProject.OutputRoot);
        Process.Start(new ProcessStartInfo
        {
            FileName = SelectedProject.OutputRoot,
            UseShellExecute = true
        });
    }

    [RelayCommand]
    private void OpenDetails()
    {
        if (SelectedProject is null) return;
        var window = App.GetService<ProjectDetailWindow>();
        window.Owner = Application.Current.MainWindow;
        window.ViewModel.Load(SelectedProject.Id);
        window.Show();
    }

    [RelayCommand]
    private async Task ToggleEnabledAsync()
    {
        if (SelectedProject is null) return;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var project = await repo.GetByIdAsync(SelectedProject.Id);
        if (project is null) return;
        project.IsEnabled = !project.IsEnabled;
        if (project.IsEnabled && project.RunMode != RunMode.Once && project.NextRunAtUtc is null)
            project.NextRunAtUtc = ScheduleCalculator.CalculateInitialNextRun(project, DateTime.UtcNow);
        await repo.UpdateAsync(project);
        await LoadProjectsAsync();
    }
}

public partial class ProjectListItem : ObservableObject
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string StartUrl { get; set; } = "";
    public string RunMode { get; set; } = "";
    public string OutputRoot { get; set; } = "";
    public bool IsEnabled { get; set; }

    [ObservableProperty]
    private string _nextRun = "—";

    [ObservableProperty]
    private string _lastStatus = "";

    [ObservableProperty]
    private string _stats = "";
}
