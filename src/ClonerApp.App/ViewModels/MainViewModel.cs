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

    [ObservableProperty]
    private string _pauseResumeLabel = "Pause";

    [ObservableProperty]
    private string _startStopLabel = "Start";

    public MainViewModel(IServiceScopeFactory scopeFactory, ICrawlEngine engine)
    {
        _scopeFactory = scopeFactory;
        _engine = engine;
        _engine.ProgressChanged += OnProgressChanged;
        _ = LoadProjectsAsync();
    }

    partial void OnSelectedProjectChanged(ProjectListItem? value) => RefreshActionLabels();

    private void RefreshActionLabels()
    {
        if (SelectedProject is null)
        {
            PauseResumeLabel = "Pause";
            StartStopLabel = "Start";
            return;
        }

        var running = _engine.IsRunning(SelectedProject.Id);
        StartStopLabel = running ? "Stop" : "Start";
        PauseResumeLabel = running && _engine.IsPaused(SelectedProject.Id) ? "Resume" : "Pause";
    }

    private static string StatusFromProgress(RunProgress e)
    {
        if (e.IsFailed) return "Failed";
        if (e.IsCancelled) return "Cancelled";
        if (e.IsCompleted) return "Completed";
        if (e.IsPaused) return "Paused";
        return "Running";
    }

    private void OnProgressChanged(object? sender, RunProgress e)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            StatusText = e.Message;
            var item = Projects.FirstOrDefault(p => p.Id == e.ProjectId);
            if (item is not null)
            {
                item.LastStatus = StatusFromProgress(e);
                if (e.RunId != Guid.Empty || e.Downloaded + e.Skipped + e.Failed + e.Filtered > 0)
                    item.Stats = $"↓ {e.Downloaded}  skip {e.Skipped}  filter {e.Filtered}  fail {e.Failed}";
            }

            if (SelectedProject?.Id == e.ProjectId)
                RefreshActionLabels();

            if (e.IsCompleted || e.IsFailed || e.IsCancelled)
                _ = LoadProjectsAsync();
        });
    }

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        var selectedId = SelectedProject?.Id;

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var runRepo = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var projects = await repo.GetAllAsync();

        Projects.Clear();
        ProjectListItem? reselect = null;
        foreach (var p in projects)
        {
            var latest = await runRepo.GetLatestForProjectAsync(p.Id);
            var status = latest?.Status.ToString() ?? "Never run";
            if (_engine.IsRunning(p.Id))
                status = _engine.IsPaused(p.Id) ? "Paused" : "Running";

            var item = new ProjectListItem
            {
                Id = p.Id,
                Name = p.Name,
                StartUrl = p.StartUrls.Split(['\r', '\n', ';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? "",
                RunMode = FormatRunMode(p.RunMode),
                OutputRoot = p.OutputRoot,
                IsEnabled = p.IsEnabled,
                NextRun = p.NextRunAtUtc?.ToLocalTime().ToString("g") ?? "-",
                LastStatus = status,
                Stats = latest is null ? "" : $"↓ {latest.Downloaded}  skip {latest.Skipped}"
            };
            Projects.Add(item);
            if (selectedId == p.Id)
                reselect = item;
        }

        SelectedProject = reselect;
        RefreshActionLabels();
    }

    private static string FormatRunMode(RunMode mode) => mode switch
    {
        RunMode.Monitor => "Watch",
        RunMode.Schedule => "Schedule",
        _ => "Once"
    };

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
        RefreshActionLabels();
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
        if (!_engine.IsRunning(SelectedProject.Id))
        {
            StatusText = "No active run to cancel.";
            return;
        }

        _engine.Cancel(SelectedProject.Id);
        SelectedProject.LastStatus = "Cancelling…";
        StatusText = "Cancel requested…";
        RefreshActionLabels();
    }

    [RelayCommand]
    private void PauseSelected()
    {
        if (SelectedProject is null) return;
        if (!_engine.IsRunning(SelectedProject.Id))
        {
            StatusText = "No active run to pause.";
            return;
        }

        if (_engine.IsPaused(SelectedProject.Id))
        {
            _engine.Resume(SelectedProject.Id);
            SelectedProject.LastStatus = "Running";
            StatusText = "Resumed.";
        }
        else
        {
            _engine.Pause(SelectedProject.Id);
            SelectedProject.LastStatus = "Paused";
            StatusText = "Paused.";
        }

        RefreshActionLabels();
    }

    [RelayCommand]
    private async Task StartOrStopSelectedAsync()
    {
        if (SelectedProject is null) return;
        if (_engine.IsRunning(SelectedProject.Id))
            CancelSelected();
        else
            await StartSelectedAsync();
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

        if (_engine.IsRunning(SelectedProject.Id))
            _engine.Cancel(SelectedProject.Id);

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
    private string _nextRun = "-";

    [ObservableProperty]
    private string _lastStatus = "";

    [ObservableProperty]
    private string _stats = "";
}
