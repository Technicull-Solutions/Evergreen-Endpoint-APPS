using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniGetUI.Avalonia.Infrastructure;
using UniGetUI.Avalonia.Views;
using UniGetUI.Core.Data;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.Core.Tools;

namespace UniGetUI.Avalonia.ViewModels;

public partial class SidebarViewModel : ViewModelBase
{
    public bool SoftwareCatalogEnabled { get; } = true;

    // ─── Badge properties ─────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UpdatesBadgeText))]
    private int _updatesBadgeCount;

    [ObservableProperty]
    private bool _updatesBadgeVisible;

    [ObservableProperty]
    private bool _bundlesBadgeVisible;

    public string UpdatesBadgeText => UpdatesBadgeCount > 99 ? "99+" : UpdatesBadgeCount.ToString();

    // When the count changes, sync the badge visibility
    partial void OnUpdatesBadgeCountChanged(int value) =>
        UpdatesBadgeVisible = value > 0;

    // ─── Loading indicators ───────────────────────────────────────────────────
    [ObservableProperty]
    private bool _discoverIsLoading;

    [ObservableProperty]
    private bool _updatesIsLoading;

    [ObservableProperty]
    private bool _installedIsLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BuildStatusIsCurrent))]
    [NotifyPropertyChangedFor(nameof(BuildStatusIsSlightlyBehind))]
    [NotifyPropertyChangedFor(nameof(BuildStatusIsBehind))]
    [NotifyPropertyChangedFor(nameof(BuildStatusIsCriticallyBehind))]
    private BuildFreshnessLevel _buildStatusLevel = BuildFreshnessLevel.Current;

    [ObservableProperty]
    private string _buildStatusText = $"{CoreTools.Translate("Status")}: {CoreTools.Translate("Up to date")}";

    public bool BuildStatusIsCurrent => BuildStatusLevel is BuildFreshnessLevel.Current;
    public bool BuildStatusIsSlightlyBehind => BuildStatusLevel is BuildFreshnessLevel.SlightlyBehind;
    public bool BuildStatusIsBehind => BuildStatusLevel is BuildFreshnessLevel.Behind;
    public bool BuildStatusIsCriticallyBehind => BuildStatusLevel is BuildFreshnessLevel.CriticallyBehind;

    // ─── Pane open/closed ─────────────────────────────────────────────────────
    // NavMenuMode: Automatic and Docked keep the pane inline so page content always reflows with
    // navigation changes. Overlay remains available as an explicit managed configuration.
    public enum NavMode { Automatic, Docked, Overlay }

    // Live window width, pushed by MainWindow's bounds observer.
    [ObservableProperty]
    private double _windowWidth = 1450;

    [ObservableProperty]
    private NavMode _mode = ParseMode(Settings.GetValue(Settings.K.NavMenuMode));

    [ObservableProperty]
    private bool _isPaneOpen;

    public static NavMode ParseMode(string value) => value switch
    {
        "docked" => NavMode.Docked,
        "overlay" => NavMode.Overlay,
        _ => NavMode.Automatic,
    };

    // Whether the pane docks inline (vs. the rail + sliding overlay).
    public bool Docked => Mode switch
    {
        NavMode.Docked => true,
        NavMode.Overlay => false,
        _ => true,
    };

    // Icon rail vs docked labeled pane vs sliding overlay — mutually exclusive.
    public bool NavDockVisible => Docked && IsPaneOpen;
    public bool RailVisible => Docked && !IsPaneOpen;
    public bool OverlayActive => !Docked && IsPaneOpen;

    private bool _wasDocked;

    public SidebarViewModel()
    {
        _wasDocked = Docked;
        _isPaneOpen = Docked && !Settings.Get(Settings.K.CollapseNavMenuOnWideScreen);
    }

    partial void OnWindowWidthChanged(double value) => ReconcileDock();

    partial void OnModeChanged(NavMode value) => ReconcileDock();

    // On a docked-state flip, open the pane by default (unless collapsed on a wide screen), or
    // collapse when leaving docked mode. Mirrors WinUI's startup IsPaneOpen logic.
    private void ReconcileDock()
    {
        bool docked = Docked;
        if (docked != _wasDocked)
        {
            _wasDocked = docked;
            IsPaneOpen = docked && !Settings.Get(Settings.K.CollapseNavMenuOnWideScreen);
        }
        RaiseLayoutChanged();
    }

    partial void OnIsPaneOpenChanged(bool value)
    {
        // Persist the collapse choice only while docked (WinUI persisted only at ≥1600px).
        if (Docked)
            Settings.Set(Settings.K.CollapseNavMenuOnWideScreen, !value);
        RaiseLayoutChanged();
    }

    private void RaiseLayoutChanged()
    {
        OnPropertyChanged(nameof(NavDockVisible));
        OnPropertyChanged(nameof(RailVisible));
        OnPropertyChanged(nameof(OverlayActive));
    }

    // ─── Selected page ────────────────────────────────────────────────────────
    [ObservableProperty]
    private PageType _selectedPageType = PageType.Null;

    // ─── Navigation ──────────────────────────────────────────────────────────
    public event EventHandler<PageType>? NavigationRequested;

    public string VersionLabel { get; } =
        $"{CoreTools.Translate("Version")} {CoreData.VersionName} · {CoreTools.Translate("Build")} {CoreData.BuildNumber}";

    public void UpdateBuildFreshness(int latestMainBuildNumber)
    {
        BuildFreshnessStatus status = BuildFreshness.Compare(
            CoreData.BuildNumber,
            latestMainBuildNumber
        );

        BuildStatusLevel = status.Level;
        BuildStatusText = status.BuildsBehind == 0
            ? $"{CoreTools.Translate("Status")}: {CoreTools.Translate("Up to date")}"
            : status.BuildsBehind == 1
                ? $"{CoreTools.Translate("Status")}: {CoreTools.Translate("1 build behind")}"
                : $"{CoreTools.Translate("Status")}: {CoreTools.Translate("{0} builds behind", status.BuildsBehind)}";
    }

    [RelayCommand]
    public void RequestNavigation(string? pageName)
    {
        if (Enum.TryParse<PageType>(pageName, out var page))
            NavigationRequested?.Invoke(this, page);
    }

    [RelayCommand]
    private static Task CheckForUpdates() =>
        AvaloniaAutoUpdater.CheckAndInstallUpdatesAsync(autoLaunch: false, manualCheck: true);

    public void SelectNavButtonForPage(PageType page)
    {
        if (SelectedPageType == page)
            OnPropertyChanged(nameof(SelectedPageType));
        else
            SelectedPageType = page;
    }

    public void SetNavItemLoading(PageType page, bool isLoading)
    {
        switch (page)
        {
            case PageType.Discover: DiscoverIsLoading = isLoading; break;
            case PageType.Updates: UpdatesIsLoading = isLoading; break;
            case PageType.Installed: InstalledIsLoading = isLoading; break;
        }
    }
}
