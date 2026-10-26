using System.Text.Json;
using Evergreen.Admin;
using UniGetUI.Avalonia.ViewModels;
using UniGetUI.Avalonia.Views;

namespace UniGetUI.Tests;

public sealed class AdminWorkspaceTests
{
    [Fact]
    public async Task AdminExportRoundTripsThroughTheEmployeeLibraryReader()
    {
        string folder = Path.Combine(Path.GetTempPath(), "evergreen-workspace-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            string json = AdminWorkspace.AddApplication(AdminWorkspace.EmptyLibrary,
                "apps", "Organization apps", "Visual Studio Code", "Microsoft.VisualStudioCode", "WinGet", "winget");
            string path = Path.Combine(folder, "SoftwareCatalog.json");
            await AdminWorkspace.ExportAsync(path, json, true);
            var catalog = Assert.Single(await SoftwareLibrary.LoadAsync(path));
            Assert.Equal("Organization apps", catalog.Name);
            Assert.Equal("Microsoft.VisualStudioCode", Assert.Single(catalog.Packages).Id);
            Assert.Equal("WinGet", catalog.Packages[0].ManagerName);
            Assert.Equal("winget", catalog.Packages[0].Source);
            // A second export replaces the workspace file without leaving temporary files.
            await AdminWorkspace.ExportAsync(path, AdminWorkspace.EmptyLibrary, true);
            Assert.Empty(Assert.Single(await SoftwareLibrary.LoadAsync(path)).Packages);
            Assert.Single(Directory.GetFiles(folder));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void DuplicateAppIdentityIsRejectedWithoutChangingTheOriginalDraft()
    {
        string json = AdminWorkspace.AddApplication(AdminWorkspace.EmptyLibrary, "apps", "Apps", "Example", "Example.App", "WinGet", "winget");
        Assert.Throws<JsonException>(() => AdminWorkspace.AddApplication(json, "apps", "Apps", "Other", "example.app", "WinGet", "WINGET"));
        Assert.Single(Assert.Single(SoftwareLibrary.Parse(json)).Packages);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"managers\":{}}")]
    [InlineData("{\"schemaVersion\":1,\"managers\":{\"Scoop\":{}}}")]
    [InlineData("{\"schemaVersion\":1,\"managers\":{\"WinGet\":{\"allowedSources\":[\"\"]}}}")]
    [InlineData("{\"schemaVersion\":1,\"managers\":{\"WinGet\":{\"executablePath\":\"relative.exe\"}}}")]
    [InlineData("{\"schemaVersion\":1,\"managers\":{\"WinGet\":null}}")]
    [InlineData("{\"schemaVersion\":1,\"managers\":{},\"unknown\":true}")]
    public void UnsupportedManagerConfigurationIsRejected(string json) =>
        Assert.Throws<JsonException>(() => AdminWorkspace.FormatManagers(json));

    [Fact]
    public void ManagerDefaultsRetainSourceAllowLists()
    {
        using var json = JsonDocument.Parse(AdminWorkspace.FormatManagers(AdminWorkspace.DefaultManagers));
        var managers = json.RootElement.GetProperty("managers");
        Assert.Equal(3, managers.EnumerateObject().Count());
        Assert.Equal(3, json.RootElement.GetProperty("maximumConcurrentUninstalls").GetInt32());
        Assert.Equal(new[] { "winget", "msstore" }, managers.GetProperty("WinGet").GetProperty("allowedSources").EnumerateArray().Select(s => s.GetString()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(100)]
    public void UninstallLimitSurvivesDraftRoundTripWithoutChangingManagers(int maximum)
    {
        string draft = AdminWorkspace.SetUninstallLimit(AdminWorkspace.DefaultManagers, maximum);
        Assert.Equal(maximum, AdminWorkspace.GetUninstallLimit(AdminWorkspace.FormatManagers(draft)));
        using var json = JsonDocument.Parse(draft);
        Assert.Equal(3, json.RootElement.GetProperty("managers").EnumerateObject().Count());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void InvalidUninstallLimitCannotBeExported(int maximum) =>
        Assert.Throws<JsonException>(() => AdminWorkspace.SetUninstallLimit(AdminWorkspace.DefaultManagers, maximum));

    [Fact]
    public async Task ExportDoesNotApplyMachineConfiguration()
    {
        await Assert.ThrowsAsync<IOException>(() => AdminWorkspace.ExportAsync(SoftwareLibrary.FilePath, AdminWorkspace.EmptyLibrary, true));
        await Assert.ThrowsAsync<IOException>(() => AdminWorkspace.ExportAsync(
            UniGetUI.Core.SettingsEngine.ManagedPackageManagerConfiguration.DefaultConfigurationPath,
            AdminWorkspace.DefaultManagers, false));
    }

    [Fact]
    public void OversizedLibraryIsRejected() => Assert.Throws<JsonException>(() =>
        AdminWorkspace.FormatLibrary(new string(' ', SoftwareLibrary.MaximumDocumentBytes + 1)));

    [Theory]
    [InlineData("")]
    [InlineData("default")]
    [InlineData("catalog")]
    [InlineData("catalog-editor")]
    [InlineData("bundles")]
    public void AppsIsEnabledAndIsTheDefaultLibraryPage(string previousSetting)
    {
        Assert.True(new SidebarViewModel().SoftwareCatalogEnabled);
        Assert.Equal(PageType.Catalog, MainWindowViewModel.ResolveStartupPage(previousSetting));
    }

    [Theory]
    [InlineData("discover", PageType.Discover)]
    [InlineData("updates", PageType.Updates)]
    [InlineData("installed", PageType.Installed)]
    public void ExplicitEmployeeStartupPreferencesArePreserved(string value, PageType expected) =>
        Assert.Equal(expected, MainWindowViewModel.ResolveStartupPage(value));
}
