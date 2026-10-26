using UniGetUI.Core.SettingsEngine;

namespace UniGetUI.Core.SettingsEngine.Tests;

public sealed class ManagedPackageManagerConfigurationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        AppContext.BaseDirectory,
        nameof(ManagedPackageManagerConfigurationTests),
        Guid.NewGuid().ToString("N")
    );
    private readonly string _configurationPath;

    public ManagedPackageManagerConfigurationTests()
    {
        Directory.CreateDirectory(_root);
        _configurationPath = Path.Combine(
            _root,
            "package-managers.json"
        );
        ManagedPackageManagerConfiguration.TEST_ConfigurationPathOverride = _configurationPath;
        ManagedPackageManagerConfiguration.TEST_PolicyOverride = new();
        ManagedPackageManagerConfiguration.Reload();
    }

    public void Dispose()
    {
        ManagedPackageManagerConfiguration.TEST_ConfigurationPathOverride = null;
        ManagedPackageManagerConfiguration.TEST_PolicyOverride = null;
        ManagedPackageManagerConfiguration.Reload();
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void SupportedManagersUseSecureDefaultsWhenNoConfigurationExists()
    {
        Assert.True(ManagedPackageManagerConfiguration.IsManagerEnabled("Winget"));
        Assert.True(ManagedPackageManagerConfiguration.IsManagerEnabled("PowerShell"));
        Assert.True(ManagedPackageManagerConfiguration.IsManagerEnabled("PowerShell7"));
        Assert.Null(ManagedPackageManagerConfiguration.IsManagerEnabled("TestManager"));
        Assert.Equal(3, ManagedPackageManagerConfiguration.MaximumConcurrentUninstalls);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(100, 100)]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(101, 1)]
    public void UninstallLimitUsesJsonWithConservativeInvalidValueHandling(int value, int expected)
    {
        File.WriteAllText(_configurationPath, "{\"schemaVersion\":1,\"maximumConcurrentUninstalls\":" + value + ",\"managers\":{}}");
        ManagedPackageManagerConfiguration.Reload();
        Assert.Equal(expected, ManagedPackageManagerConfiguration.MaximumConcurrentUninstalls);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    [InlineData(160, false)]
    public void UninstallSelectionsMustFitTheEffectiveLimit(int count, bool allowed) =>
        Assert.Equal(allowed, ManagedPackageManagerConfiguration.IsUninstallSelectionAllowed(count));

    [Theory]
    [InlineData(2, 2)]
    [InlineData(8, 8)]
    [InlineData(0, 1)]
    public void PolicyUninstallLimitOverridesJson(int policy, int expected)
    {
        File.WriteAllText(_configurationPath, """{"schemaVersion":1,"maximumConcurrentUninstalls":5,"managers":{}}""");
        ManagedPackageManagerConfiguration.TEST_PolicyOverride = new() { MaximumConcurrentUninstalls = policy };
        ManagedPackageManagerConfiguration.Reload();
        Assert.Equal(expected, ManagedPackageManagerConfiguration.MaximumConcurrentUninstalls);
    }

    [Theory]
    [InlineData("Winget")]
    [InlineData("WinGet")]
    [InlineData("PowerShell")]
    [InlineData("PowerShell7")]
    public void ProductManagersAreAdministratorManaged(string managerName)
    {
        Assert.True(
            ManagedPackageManagerConfiguration.IsAdministratorManagedManager(managerName)
        );
    }

    [Fact]
    public void UnrelatedManagerIsNotAdministratorManaged()
    {
        Assert.False(
            ManagedPackageManagerConfiguration.IsAdministratorManagedManager("TestManager")
        );
    }

    [Fact]
    public void JsonControlsManagerStateAndAllowedSources()
    {
        File.WriteAllText(
            _configurationPath,
            """
            {
              "schemaVersion": 1,
              "managers": {
                "WinGet": {
                  "enabled": false,
                  "allowedSources": ["winget"]
                }
              }
            }
            """
        );
        ManagedPackageManagerConfiguration.Reload();

        Assert.False(ManagedPackageManagerConfiguration.IsManagerEnabled("Winget"));
        Assert.True(ManagedPackageManagerConfiguration.IsSourceAllowed("Winget", "winget"));
        Assert.False(ManagedPackageManagerConfiguration.IsSourceAllowed("Winget", "msstore"));
    }

    [Fact]
    public void GroupPolicyOverridesJsonPerSetting()
    {
        File.WriteAllText(
            _configurationPath,
            """
            {
              "schemaVersion": 1,
              "managers": {
                "PowerShell7": {
                  "enabled": false,
                  "allowedSources": ["Internal"]
                }
              }
            }
            """
        );
        ManagedPackageManagerConfiguration.TEST_PolicyOverride = new()
        {
            PowerShell7Enabled = true,
            PowerShell7AllowedSources = ["PSGallery"],
            PowerShell7ExecutablePath = @"C:\Program Files\PowerShell\7\pwsh.exe",
        };
        ManagedPackageManagerConfiguration.Reload();

        Assert.True(ManagedPackageManagerConfiguration.IsManagerEnabled("PowerShell7"));
        Assert.True(ManagedPackageManagerConfiguration.IsSourceAllowed("PowerShell7", "PSGallery"));
        Assert.False(ManagedPackageManagerConfiguration.IsSourceAllowed("PowerShell7", "Internal"));
        Assert.Equal(
            @"C:\Program Files\PowerShell\7\pwsh.exe",
            ManagedPackageManagerConfiguration.GetExecutablePath("PowerShell7")
        );
    }

    [Fact]
    public void EmptyAllowedSourceListBlocksEverySource()
    {
        ManagedPackageManagerConfiguration.TEST_PolicyOverride = new()
        {
            WinGetAllowedSources = [],
        };
        ManagedPackageManagerConfiguration.Reload();

        Assert.False(ManagedPackageManagerConfiguration.IsSourceAllowed("WinGet", "winget"));
        Assert.False(ManagedPackageManagerConfiguration.IsSourceAllowed("WinGet", "msstore"));
    }
}
