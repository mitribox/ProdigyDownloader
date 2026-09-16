using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ClonerApp.App.ViewModels;

public partial class ProjectDetailViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICrawlEngine _engine;
    private Guid _projectId;

    public ObservableCollection<string> LogLines { get; } = new();

    [ObservableProperty] private string _title = "Project";
    [ObservableProperty] private string _stats = "";
    [ObservableProperty] private string _outputRoot = "";
    [ObservableProperty] private int _downloaded;
    [ObservableProperty] private int _skipped;
    [ObservableProperty] private int _failed;
    [ObservableProperty] private int _filtered;
    [ObservableProperty] private int _pages;
    [ObservableProperty] private int _found;

    public ProjectDetailViewModel(IServiceScopeFactory scopeFactory, ICrawlEngine engine)
    {
        _scopeFactory = scopeFactory;
        _engine = engine;
        _engine.ProgressChanged += OnProgress;
    }

    public async void Load(Guid projectId)
    {
        _projectId = projectId;
        using var scope = _scopeFactory.CreateScope();
        var projects = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
        var runs = scope.ServiceProvider.GetRequiredService<IRunRepository>();
        var project = await projects.GetByIdAsync(projectId);
        if (project is null) return;

        Title = project.Name;
        OutputRoot = project.OutputRoot;
        var latest = await runs.GetLatestForProjectAsync(projectId);
        if (latest is not null)
        {
            Pages = latest.PagesCrawled;
            Found = latest.MediaFound;
            Downloaded = latest.Downloaded;
            Skipped = latest.Skipped;
            Failed = latest.Failed;
            Filtered = latest.Filtered;
            Stats = latest.Status.ToString();
        }
    }

    private void OnProgress(object? sender, RunProgress e)
    {
        if (e.ProjectId != _projectId) return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            Pages = e.PagesCrawled;
            Found = e.MediaFound;
            Downloaded = e.Downloaded;
            Skipped = e.Skipped;
            Failed = e.Failed;
            Filtered = e.Filtered;
            Stats = e.IsFailed ? "Failed"
                : e.IsCancelled ? "Cancelled"
                : e.IsCompleted ? "Completed"
                : e.IsPaused ? "Paused"
                : "Running";
            LogLines.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {e.Message}");
            while (LogLines.Count > 500) LogLines.RemoveAt(LogLines.Count - 1);
        });
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (string.IsNullOrWhiteSpace(OutputRoot)) return;
        if (!Directory.Exists(OutputRoot)) Directory.CreateDirectory(OutputRoot);
        Process.Start(new ProcessStartInfo { FileName = OutputRoot, UseShellExecute = true });
    }

    [RelayCommand]
    private void Cancel() => _engine.Cancel(_projectId);

    [RelayCommand]
    private void Pause()
    {
        if (!_engine.IsRunning(_projectId)) return;
        if (_engine.IsPaused(_projectId))
            _engine.Resume(_projectId);
        else
            _engine.Pause(_projectId);
    }
}
