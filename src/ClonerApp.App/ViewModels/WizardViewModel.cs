using System.Windows;
using ClonerApp.Core.Enums;
using ClonerApp.Core.Interfaces;
using ClonerApp.Core.Models;
using ClonerApp.Engine;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace ClonerApp.App.ViewModels;

public partial class WizardViewModel : ObservableObject
{
    private const int LastStep = 8;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICrawlEngine _engine;

    public event EventHandler? CloseRequested;

    public ProjectConfigViewModel Config { get; } = new();

    [ObservableProperty] private int _stepIndex;
    [ObservableProperty] private string _stepTitle = "Starting addresses";
    [ObservableProperty] private bool _startNow = true;
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private string? _validationError;
    [ObservableProperty] private bool _isBusy;

    public bool CanGoBack => StepIndex > 0;
    public bool CanGoNext => StepIndex < LastStep;
    public bool IsLastStep => StepIndex == LastStep;

    public WizardViewModel(IServiceScopeFactory scopeFactory, ICrawlEngine engine)
    {
        _scopeFactory = scopeFactory;
        _engine = engine;
        UpdateStepTitle();
    }

    partial void OnStepIndexChanged(int value)
    {
        UpdateStepTitle();
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(IsLastStep));
        if (value == LastStep) Summary = Config.BuildSummary();
    }

    private void UpdateStepTitle()
    {
        StepTitle = StepIndex switch
        {
            0 => "1. Starting addresses",
            1 => "2. Scan settings",
            2 => "3. Media types",
            3 => "4. Filters (optional)",
            4 => "5. Exclude if",
            5 => "6. Performance",
            6 => "7. Storage",
            7 => "8. Run mode",
            _ => "9. Review"
        };
    }

    [RelayCommand]
    private void Next()
    {
        ValidationError = ValidateCurrentStep();
        if (ValidationError is not null) return;
        if (StepIndex < LastStep) StepIndex++;
    }

    [RelayCommand]
    private void Back()
    {
        ValidationError = null;
        if (StepIndex > 0) StepIndex--;
    }

    [RelayCommand]
    private async Task FinishAsync()
    {
        ValidationError = Config.Validate();
        if (ValidationError is not null) return;

        IsBusy = true;
        try
        {
            var project = new Project();
            Config.ApplyTo(project);

            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IProjectRepository>();
            await repo.AddAsync(project);

            if (StartNow || project.RunMode == RunMode.Once)
            {
                var id = project.Id;
                _ = Task.Run(async () =>
                {
                    try { await _engine.RunProjectAsync(id); }
                    catch { /* surfaced via progress */ }
                });
            }

            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ValidationError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private string? ValidateCurrentStep() => StepIndex switch
    {
        0 => ValidateAddresses(),
        2 when !Config.ExtensionOptions.Any(x => x.IsSelected) => "Select at least one file extension.",
        6 when string.IsNullOrWhiteSpace(Config.OutputRoot) => "Output folder is required.",
        7 when Config.RunMode == RunMode.Monitor && Config.MonitorIntervalMinutes < 1 =>
            "Watch interval must be at least 1 minute.",
        7 when Config.RunMode == RunMode.Schedule && !TimeSpan.TryParse(Config.ScheduleTime, out _) =>
            "Schedule time must be HH:mm.",
        8 => Config.Validate(),
        _ => null
    };

    private string? ValidateAddresses()
    {
        if (string.IsNullOrWhiteSpace(Config.ProjectName))
            return "Project name is required.";
        var err = Config.Validate();
        if (err is null) return null;
        if (err.Contains("URL", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("regex", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("Advanced", StringComparison.OrdinalIgnoreCase) ||
            err.Contains("name", StringComparison.OrdinalIgnoreCase))
            return err;
        return null;
    }
}
