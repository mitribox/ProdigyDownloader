using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using ClonerApp.Core;
using ClonerApp.Core.Enums;
using ClonerApp.Core.Models;
using ClonerApp.Engine;
using ClonerApp.Engine.Crawling;
using ClonerApp.Engine.Filtering;
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
    public ObservableCollection<ExcludeRuleRowViewModel> ExcludeRules { get; } = new();
    public ObservableCollection<string> SettingSections { get; } = new()
    {
        "General",
        "Starting Addresses",
        "Scan Settings",
        "File Types",
        "Size Filters",
        "Exclude Rules",
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
    [ObservableProperty] private bool _crawlEntireSite;
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
    public Array UrlInputModes => Enum.GetValues(typeof(UrlInputMode));
    public string[] SizeUnits { get; } = ["KB", "MB"];

    public IReadOnlyList<NamedChoice<RunMode>> RunModeChoices { get; } =
    [
        new(RunMode.Once, "Download once"),
        new(RunMode.Schedule, "Run on a schedule"),
        new(RunMode.Monitor, "Watch for new posts")
    ];

    public IReadOnlyList<NamedChoice<ExcludeField>> ExcludeFieldChoices { get; } =
    [
        new(ExcludeField.FileName, "File name"),
        new(ExcludeField.PageTitle, "Page title"),
        new(ExcludeField.Tag, "Tag"),
        new(ExcludeField.Url, "URL")
    ];

    public IReadOnlyList<NamedChoice<ExcludeOperator>> ExcludeOperatorChoices { get; } =
    [
        new(ExcludeOperator.Contains, "contains"),
        new(ExcludeOperator.Equals, "="),
        new(ExcludeOperator.MatchesRegex, "matches regex"),
        new(ExcludeOperator.DoesNotMatchRegex, "does not match regex"),
        new(ExcludeOperator.DoesNotContain, "does not contain")
    ];

    public IReadOnlyList<NamedChoice<RuleJoin>> RuleJoinChoices { get; } =
    [
        new(RuleJoin.And, "AND"),
        new(RuleJoin.Or, "OR")
    ];

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
    public bool IsExcludeRules => SelectedSection == "Exclude Rules";
    public bool IsDownloadLimits => SelectedSection == "Download Limits";
    public bool IsStorage => SelectedSection == "Storage";
    public bool IsSchedule => SelectedSection == "Schedule";
    public bool ShowLimitedScanControls => !CrawlEntireSite;
    public bool IsRunOnce => RunMode == RunMode.Once;
    public bool IsRunWatch => RunMode == RunMode.Monitor;
    public bool IsRunSchedule => RunMode == RunMode.Schedule;

    public ProjectConfigViewModel()
    {
        RebuildExtensions();
    }

    partial void OnCrawlEntireSiteChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowLimitedScanControls));
        if (value)
            SameDomainOnly = true;
    }

    partial void OnRunModeChanged(RunMode value)
    {
        OnPropertyChanged(nameof(IsRunOnce));
        OnPropertyChanged(nameof(IsRunWatch));
        OnPropertyChanged(nameof(IsRunSchedule));
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
        OnPropertyChanged(nameof(IsExcludeRules));
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
        CrawlEntireSite = project.CrawlEntireSite;
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
        LoadExcludeRules(project.ExcludeRulesJson);

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
            return "Watch interval must be at least 1 minute.";

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
        project.CrawlEntireSite = CrawlEntireSite;
        project.SameDomainOnly = CrawlEntireSite || SameDomainOnly;
        project.HonorRobotsTxt = HonorRobotsTxt;
        project.ScanWithinStartingFolder = CrawlEntireSite ? false : ScanWithinStartingFolder;
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
        project.ExcludeRulesJson = ExcludeRules.Count == 0
            ? null
            : ExcludeRuleEvaluator.SerializeRules(ExcludeRules.Select(r => r.ToModel()));

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
            $"Depth: {(CrawlEntireSite ? "entire site" : MaxDepth.ToString())}, Max pages: {(CrawlEntireSite ? "entire site" : MaxPages.ToString())}, Same domain: {(CrawlEntireSite || SameDomainOnly)}\n" +
            $"Scan within folder: {(CrawlEntireSite ? false : ScanWithinStartingFolder)}, Ignore home: {IgnoreHomePage}, Scan image links: {AlwaysScanImageLinks}\n" +
            $"Media: {MediaCategory} [{exts}]\n" +
            $"Filters: {filterText}\n" +
            $"Exclude rules: {(ExcludeRules.Count == 0 ? "none" : ExcludeRules.Count.ToString())}\n" +
            $"Connections: {MaxConnections}, Delay: {PageDelayMs}ms\n" +
            $"Output folder: {OutputRoot}\n" +
            $"Storage: {(StorageLayout == StorageLayout.PageTitle ? "By page title" : "Single folder")}\n" +
            $"Mode: {RunModeLabel}" +
            (RunMode == RunMode.Monitor ? $" every {MonitorIntervalMinutes} min" : "") +
            (RunMode == RunMode.Schedule ? $" at {ScheduleTime}" : "");
    }

    private string RunModeLabel => RunMode switch
    {
        RunMode.Monitor => "Watch for new posts",
        RunMode.Schedule => "Run on a schedule",
        _ => "Download once"
    };

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

    [RelayCommand]
    private void AddExcludeRule()
    {
        ExcludeRules.Add(new ExcludeRuleRowViewModel
        {
            JoinWithPrevious = RuleJoin.Or,
            IsFirst = ExcludeRules.Count == 0
        });
        RefreshExcludeRuleFlags();
    }

    [RelayCommand]
    private void RemoveExcludeRule(ExcludeRuleRowViewModel? rule)
    {
        if (rule is null) return;
        ExcludeRules.Remove(rule);
        RefreshExcludeRuleFlags();
    }

    [RelayCommand]
    private void ClearExcludeRules()
    {
        ExcludeRules.Clear();
    }

    private void LoadExcludeRules(string? json)
    {
        ExcludeRules.Clear();
        foreach (var rule in ExcludeRuleEvaluator.ParseRules(json))
        {
            ExcludeRules.Add(ExcludeRuleRowViewModel.FromModel(rule));
        }
        RefreshExcludeRuleFlags();
    }

    private void RefreshExcludeRuleFlags()
    {
        for (var i = 0; i < ExcludeRules.Count; i++)
            ExcludeRules[i].IsFirst = i == 0;
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

public partial class ExcludeRuleRowViewModel : ObservableObject
{
    [ObservableProperty] private ExcludeField _field = ExcludeField.FileName;
    [ObservableProperty] private ExcludeOperator _operator = ExcludeOperator.Contains;
    [ObservableProperty] private string _value = "";
    [ObservableProperty] private RuleJoin _joinWithPrevious = RuleJoin.Or;
    [ObservableProperty] private bool _isFirst = true;

    public bool ShowJoin => !IsFirst;

    partial void OnIsFirstChanged(bool value) => OnPropertyChanged(nameof(ShowJoin));

    public ExcludeRule ToModel() => new()
    {
        Field = Field,
        Operator = Operator,
        Value = Value ?? "",
        JoinWithPrevious = JoinWithPrevious
    };

    public static ExcludeRuleRowViewModel FromModel(ExcludeRule rule) => new()
    {
        Field = rule.Field,
        Operator = rule.Operator,
        Value = rule.Value ?? "",
        JoinWithPrevious = rule.JoinWithPrevious
    };
}

public sealed record StorageChoice(StorageLayout Value, string Label);
public sealed record NamedChoice<T>(T Value, string Label);
