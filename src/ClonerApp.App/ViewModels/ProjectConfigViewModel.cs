using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using ClonerApp.Core;
using ClonerApp.Core.Enums;
using ClonerApp.Core.Models;
using ClonerApp.Engine;
using ClonerApp.Engine.Crawling;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;

namespace ClonerApp.App.ViewModels;

/// <summary>
/// Shared full project configuration used by the create wizard and edit settings UI.
/// </summary>
public partial class ProjectConfigViewModel : ObservableObject
{
    public ObservableCollection<ExtensionOption> ExtensionOptions { get; } = new();
    public ObservableCollection<string> SettingSections { get; } = new()
    {
        "General",
        "Starting Addresses",
        "Scan Settings",
        "File Types",
        "Size Filters",
        "Download Limits",
        "Storage",
        "Schedule"
    };

    [ObservableProperty] private string _selectedSection = "General";
    [ObservableProperty] private string _projectName = "New Project";
    [ObservableProperty] private UrlInputMode _urlInputMode = UrlInputMode.Simple;
    [ObservableProperty] private string _startUrls = "https://";
    [ObservableProperty] private string? _urlRegex;
    [ObservableProperty] private int _maxDepth = 2;
    [ObservableProperty] private int _maxPages = 200;
    [ObservableProperty] private bool _sameDomainOnly = true;
    [ObservableProperty] private bool _honorRobotsTxt = true;
    [ObservableProperty] private bool _scanWithinStartingFolder;
    [ObservableProperty] private bool _ignoreHomePage;
    [ObservableProperty] private bool _alwaysScanImageLinks = true;
    [ObservableProperty] private MediaCategory _mediaCategory = MediaCategory.Both;
    [ObservableProperty] private string? _minFileSizeText;
    [ObservableProperty] private string _minFileSizeUnit = "KB";
    [ObservableProperty] private string? _minWidthText;
    [ObservableProperty] private string? _minHeightText;
    [ObservableProperty] private int _maxConnections = 4;
    [ObservableProperty] private int _pageDelayMs;
    [ObservableProperty] private string _outputRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ProdigyDownloader");
    [ObservableProperty] private StorageLayout _storageLayout = StorageLayout.PageTitle;
    [ObservableProperty] private RunMode _runMode = RunMode.Once;
    [ObservableProperty] private int _monitorIntervalMinutes = 30;
    [ObservableProperty] private string _scheduleTime = "02:00";
    [ObservableProperty] private bool _scheduleMon = true;
    [ObservableProperty] private bool _scheduleTue = true;
    [ObservableProperty] private bool _scheduleWed = true;
    [ObservableProperty] private bool _scheduleThu = true;
    [ObservableProperty] private bool _scheduleFri = true;
    [ObservableProperty] private bool _scheduleSat = true;
    [ObservableProperty] private bool _scheduleSun = true;
    [ObservableProperty] private bool _deduplicateByHash = true;
    [ObservableProperty] private bool _versionOnChange = true;
    [ObservableProperty] private bool _isEnabled = true;
    [ObservableProperty] private string? _validationError;

    public Array MediaCategories => Enum.GetValues(typeof(MediaCategory));
    public Array RunModes => Enum.GetValues(typeof(RunMode));
    public Array UrlInputModes => Enum.GetValues(typeof(UrlInputMode));
    public string[] SizeUnits { get; } = ["KB", "MB"];

    public IReadOnlyList<StorageChoice> StorageChoices { get; } =
    [
        new(StorageLayout.Flat, "A. Put all files into a single folder"),
        new(StorageLayout.PageTitle, "B. Store files in folders by page title")
    ];

    public bool IsSimpleUrlMode => UrlInputMode == UrlInputMode.Simple;
    public bool IsAdvancedUrlMode => UrlInputMode == UrlInputMode.Advanced;
    public bool IsGeneral => SelectedSection == "General";
    public bool IsStartingAddresses => SelectedSection == "Starting Addresses";
    public bool IsScanSettings => SelectedSection == "Scan Settings";
    public bool IsFileTypes => SelectedSection == "File Types";
    public bool IsSizeFilters => SelectedSection == "Size Filters";
    public bool IsDownloadLimits => SelectedSection == "Download Limits";
    public bool IsStorage => SelectedSection == "Storage";
    public bool IsSchedule => SelectedSection == "Schedule";

    public ProjectConfigViewModel()
    {
        RebuildExtensions();
    }

    partial void OnUrlInputModeChanged(UrlInputMode value)
    {
        OnPropertyChanged(nameof(IsSimpleUrlMode));
        OnPropertyChanged(nameof(IsAdvancedUrlMode));
    }

    partial void OnSelectedSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsGeneral));
        OnPropertyChanged(nameof(IsStartingAddresses));
        OnPropertyChanged(nameof(IsScanSettings));
        OnPropertyChanged(nameof(IsFileTypes));
        OnPropertyChanged(nameof(IsSizeFilters));
        OnPropertyChanged(nameof(IsDownloadLimits));
        OnPropertyChanged(nameof(IsStorage));
        OnPropertyChanged(nameof(IsSchedule));
    }

    partial void OnMediaCategoryChanged(MediaCategory value) => RebuildExtensions(preserveSelection: true);

    public void LoadFrom(Project project)
    {
        ProjectName = project.Name;
        UrlInputMode = project.UrlInputMode;
        StartUrls = project.StartUrls;
        UrlRegex = project.UrlRegex;
        MaxDepth = project.MaxDepth;
        MaxPages = project.MaxPages;
        SameDomainOnly = project.SameDomainOnly;
        HonorRobotsTxt = project.HonorRobotsTxt;
        ScanWithinStartingFolder = project.ScanWithinStartingFolder;
        IgnoreHomePage = project.IgnoreHomePage;
        AlwaysScanImageLinks = project.AlwaysScanImageLinks;
        MediaCategory = project.MediaCategory;
        RebuildExtensions(preserveSelection: false);

        var selected = (project.SelectedExtensions ?? "")
            .Split([',', ';', ' ', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.TrimStart('.').ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (selected.Count > 0)
        {
            foreach (var opt in ExtensionOptions)
                opt.IsSelected = selected.Contains(opt.Extension);
        }

        if (project.MinFileSizeBytes is > 0)
        {
            if (project.MinFileSizeBytes % (1024L * 1024L) == 0)
            {
                MinFileSizeUnit = "MB";
                MinFileSizeText = (project.MinFileSizeBytes.Value / (1024L * 1024L)).ToString();
            }
            else
            {
                MinFileSizeUnit = "KB";
                MinFileSizeText = (project.MinFileSizeBytes.Value / 1024L).ToString();
            }
        }
        else
        {
            MinFileSizeText = null;
        }

        MinWidthText = project.MinWidth?.ToString();
        MinHeightText = project.MinHeight?.ToString();
        MaxConnections = project.MaxConnections;
        PageDelayMs = project.PageDelayMs;
        OutputRoot = project.OutputRoot;
        StorageLayout = project.StorageLayout is StorageLayout.PageTitle or StorageLayout.Flat
            ? project.StorageLayout
            : StorageLayout.PageTitle;
        RunMode = project.RunMode;
        MonitorIntervalMinutes = project.MonitorIntervalMinutes ?? 30;
        ScheduleTime = project.ScheduleTime ?? "02:00";
        DeduplicateByHash = project.DeduplicateByHash;
        VersionOnChange = project.VersionOnChange;
        IsEnabled = project.IsEnabled;

        var days = (project.ScheduleDays ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (days.Count == 0)
        {
            ScheduleMon = ScheduleTue = ScheduleWed = ScheduleThu = ScheduleFri = ScheduleSat = ScheduleSun = true;
        }
        else
        {
            ScheduleMon = days.Contains(nameof(DayOfWeek.Monday));
            ScheduleTue = days.Contains(nameof(DayOfWeek.Tuesday));
            ScheduleWed = days.Contains(nameof(DayOfWeek.Wednesday));
            ScheduleThu = days.Contains(nameof(DayOfWeek.Thursday));
            ScheduleFri = days.Contains(nameof(DayOfWeek.Friday));
            ScheduleSat = days.Contains(nameof(DayOfWeek.Saturday));
            ScheduleSun = days.Contains(nameof(DayOfWeek.Sunday));
        }
    }

    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(ProjectName))
            return "Project name is required.";

        if (UrlSeedExpander.ExpandSimple(StartUrls).Count == 0)
            return "Enter at least one valid http(s) URL.";

        if (UrlInputMode == UrlInputMode.Advanced)
        {
            if (string.IsNullOrWhiteSpace(UrlRegex))
                return "Advanced mode requires a URL regex (e.g. ^https://xyz\\.com/blog\\.php/).";
            try { _ = new Regex(UrlRegex); }
            catch { return "URL regex is invalid."; }
        }
        else if (!string.IsNullOrWhiteSpace(UrlRegex))
        {
            try { _ = new Regex(UrlRegex); }
            catch { return "URL regex is invalid."; }
        }

        if (!ExtensionOptions.Any(x => x.IsSelected))
            return "Select at least one file extension.";

        if (string.IsNullOrWhiteSpace(OutputRoot))
            return "Output folder is required.";

        if (RunMode == RunMode.Monitor && MonitorIntervalMinutes < 1)
            return "Monitor interval must be at least 1 minute.";

        if (RunMode == RunMode.Schedule && !TimeSpan.TryParse(ScheduleTime, out _))
            return "Schedule time must be HH:mm.";

        return null;
    }

    public void ApplyTo(Project project)
    {
        var selected = ExtensionOptions.Where(x => x.IsSelected).Select(x => x.Extension).ToList();
        long? minBytes = null;
        if (long.TryParse(MinFileSizeText, out var size) && size > 0)
            minBytes = MinFileSizeUnit == "MB" ? size * 1024L * 1024L : size * 1024L;

        int? minW = int.TryParse(MinWidthText, out var w) && w > 0 ? w : null;
        int? minH = int.TryParse(MinHeightText, out var h) && h > 0 ? h : null;

        var days = new List<string>();
        if (ScheduleMon) days.Add(nameof(DayOfWeek.Monday));
        if (ScheduleTue) days.Add(nameof(DayOfWeek.Tuesday));
        if (ScheduleWed) days.Add(nameof(DayOfWeek.Wednesday));
        if (ScheduleThu) days.Add(nameof(DayOfWeek.Thursday));
        if (ScheduleFri) days.Add(nameof(DayOfWeek.Friday));
        if (ScheduleSat) days.Add(nameof(DayOfWeek.Saturday));
        if (ScheduleSun) days.Add(nameof(DayOfWeek.Sunday));

        project.Name = ProjectName.Trim();
        project.StartUrls = StartUrls.Trim();
        project.UrlInputMode = UrlInputMode;
        project.UrlPrefix = null;
        project.PageFrom = null;
        project.PageTo = null;
        project.UrlRegex = string.IsNullOrWhiteSpace(UrlRegex) ? null : UrlRegex.Trim();
        project.MaxDepth = MaxDepth;
        project.MaxPages = MaxPages;
        project.SameDomainOnly = SameDomainOnly;
        project.HonorRobotsTxt = HonorRobotsTxt;
        project.ScanWithinStartingFolder = ScanWithinStartingFolder;
        project.IgnoreHomePage = IgnoreHomePage;
        project.AlwaysScanImageLinks = AlwaysScanImageLinks;
        project.MediaCategory = MediaCategory;
        project.SelectedExtensions = string.Join(',', selected);
        project.MinFileSizeBytes = minBytes;
        project.MinWidth = minW;
        project.MinHeight = minH;
        project.MaxConnections = Math.Clamp(MaxConnections, 1, 16);
        project.PageDelayMs = Math.Max(0, PageDelayMs);
        project.OutputRoot = OutputRoot.Trim();
        project.StorageLayout = StorageLayout is StorageLayout.PageTitle or StorageLayout.Flat
            ? StorageLayout
            : StorageLayout.PageTitle;
        project.RunMode = RunMode;
        project.MonitorIntervalMinutes = MonitorIntervalMinutes;
        project.ScheduleTime = ScheduleTime;
        project.ScheduleDays = string.Join(',', days);
        project.DeduplicateByHash = DeduplicateByHash;
        project.VersionOnChange = VersionOnChange;
        project.IsEnabled = IsEnabled;

        if (project.RunMode == RunMode.Once)
            project.NextRunAtUtc = null;
        else
            project.NextRunAtUtc = ScheduleCalculator.CalculateInitialNextRun(project, DateTime.UtcNow);

        Directory.CreateDirectory(project.OutputRoot);
    }

    public string BuildSummary()
    {
        var exts = string.Join(", ", ExtensionOptions.Where(x => x.IsSelected).Select(x => x.Extension));
        var filters = new List<string>();
        if (!string.IsNullOrWhiteSpace(MinFileSizeText)) filters.Add($"min size {MinFileSizeText} {MinFileSizeUnit}");
        if (!string.IsNullOrWhiteSpace(MinWidthText)) filters.Add($"min width {MinWidthText}px");
        if (!string.IsNullOrWhiteSpace(MinHeightText)) filters.Add($"min height {MinHeightText}px");
        var filterText = filters.Count == 0 ? "none" : string.Join(", ", filters);

        return
            $"Name: {ProjectName}\n" +
            $"URL mode: {UrlInputMode}\n" +
            $"URL(s): {StartUrls}\n" +
            (string.IsNullOrWhiteSpace(UrlRegex) ? "" : $"Regex: {UrlRegex}\n") +
            $"Depth: {MaxDepth}, Max pages: {MaxPages}, Same domain: {SameDomainOnly}\n" +
            $"Scan within folder: {ScanWithinStartingFolder}, Ignore home: {IgnoreHomePage}, Scan image links: {AlwaysScanImageLinks}\n" +
            $"Media: {MediaCategory} [{exts}]\n" +
            $"Filters: {filterText}\n" +
            $"Connections: {MaxConnections}, Delay: {PageDelayMs}ms\n" +
            $"Output folder: {OutputRoot}\n" +
            $"Storage: {(StorageLayout == StorageLayout.PageTitle ? "By page title" : "Single folder")}\n" +
            $"Mode: {RunMode}" +
            (RunMode == RunMode.Monitor ? $" every {MonitorIntervalMinutes} min" : "") +
            (RunMode == RunMode.Schedule ? $" at {ScheduleTime}" : "");
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select output folder",
            InitialDirectory = Directory.Exists(OutputRoot) ? OutputRoot : null
        };
        if (dialog.ShowDialog() == true)
            OutputRoot = dialog.FolderName;
    }

    private void RebuildExtensions(bool preserveSelection = false)
    {
        var previouslySelected = preserveSelection
            ? ExtensionOptions.Where(x => x.IsSelected).Select(x => x.Extension).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : null;

        var defaults = MediaExtensions.DefaultFor(MediaCategory).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var all = MediaCategory switch
        {
            MediaCategory.Photos => MediaExtensions.PhotoExtensions,
            MediaCategory.Videos => MediaExtensions.VideoExtensions,
            _ => MediaExtensions.PhotoExtensions.Concat(MediaExtensions.VideoExtensions)
        };

        ExtensionOptions.Clear();
        foreach (var ext in all.OrderBy(x => x))
        {
            var selected = previouslySelected is null
                ? defaults.Contains(ext)
                : previouslySelected.Contains(ext);
            ExtensionOptions.Add(new ExtensionOption { Extension = ext, IsSelected = selected });
        }
    }
}

public partial class ExtensionOption : ObservableObject
{
    public string Extension { get; set; } = "";

    [ObservableProperty]
    private bool _isSelected;
}

public sealed record StorageChoice(StorageLayout Value, string Label);
