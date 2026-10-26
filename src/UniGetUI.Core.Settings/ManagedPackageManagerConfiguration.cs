using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using UniGetUI.Core.Logging;

namespace UniGetUI.Core.SettingsEngine;

/// <summary>
/// Resolves administrator-owned package-manager configuration. Group Policy values override the
/// machine JSON document; missing values use the secure product defaults. Per-user settings are
/// deliberately outside this configuration path.
/// </summary>
public static class ManagedPackageManagerConfiguration
{
    private const long MaximumConfigurationFileSize = 1024 * 1024;
    public const int DefaultMaximumConcurrentUninstalls = 3;
    public const int MaximumSupportedConcurrentUninstalls = 100;

    public static int MaximumConcurrentUninstalls => GetSnapshot().MaximumConcurrentUninstalls;

    public static bool IsUninstallSelectionAllowed(int count) => count > 0 && count <= MaximumConcurrentUninstalls;

    public const string PolicyRegistryPath =
        @"Software\Policies\Technicull Solutions\Evergreen Endpoint\Package Manager\PackageManagers";

    public static readonly string DefaultConfigurationPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Technicull Solutions",
        "Evergreen Endpoint",
        "Package Manager",
        "package-managers.json"
    );

    private static readonly Lock ConfigurationLock = new();
    private static ManagedPackageManagerConfigurationSnapshot? __snapshot;

    public static string? TEST_ConfigurationPathOverride { private get; set; }
    public static ManagedPackageManagerPolicy? TEST_PolicyOverride { private get; set; }

    public static bool? IsManagerEnabled(string managerName)
    {
        ManagedPackageManagerEntry? entry = GetSnapshot().FindManager(managerName);
        return entry?.Enabled;
    }

    public static bool IsAdministratorManagedManager(string managerName) =>
        ManagedPackageManagerConfigurationSnapshot.NormalizeManagerName(managerName) is not null;

    public static bool IsSourceAllowed(string managerName, string sourceName)
    {
        ManagedPackageManagerEntry? entry = GetSnapshot().FindManager(managerName);
        if (entry?.AllowedSources is not { } allowedSources)
            return true;

        return allowedSources.Contains(sourceName, StringComparer.OrdinalIgnoreCase);
    }

    public static string? GetExecutablePath(string managerName) =>
        GetSnapshot().FindManager(managerName)?.ExecutablePath;

    public static void Reload()
    {
        lock (ConfigurationLock)
            __snapshot = null;
    }

    private static ManagedPackageManagerConfigurationSnapshot GetSnapshot()
    {
        lock (ConfigurationLock)
            return __snapshot ??= LoadSnapshot();
    }

    private static ManagedPackageManagerConfigurationSnapshot LoadSnapshot()
    {
        ManagedPackageManagerPolicy policy = TEST_PolicyOverride
            ?? (OperatingSystem.IsWindows() ? ReadWindowsPolicy() : new());
        string configurationPath = TEST_ConfigurationPathOverride
            ?? policy.ConfigurationFile
            ?? DefaultConfigurationPath;

        ManagedPackageManagerConfigurationDocument? document = ReadJson(configurationPath);
        return new ManagedPackageManagerConfigurationSnapshot(document, policy);
    }

    private static ManagedPackageManagerConfigurationDocument? ReadJson(string path)
    {
        if (!Path.IsPathFullyQualified(path))
        {
            Logger.Warn("Ignoring a package-manager configuration path that is not absolute");
            return null;
        }

        if (!File.Exists(path))
            return null;

        try
        {
            if (new FileInfo(path).Length > MaximumConfigurationFileSize)
            {
                Logger.Warn($"Ignoring oversized package-manager configuration at {path}");
                return null;
            }

            string json = File.ReadAllText(path);
            ManagedPackageManagerConfigurationDocument? document = JsonSerializer.Deserialize(
                json,
                ManagedPackageManagerJsonContext.Default.ManagedPackageManagerConfigurationDocument
            );
            if (document?.SchemaVersion != 1)
            {
                Logger.Warn($"Ignoring unsupported package-manager configuration schema in {path}");
                return null;
            }

            return document;
        }
        catch (Exception ex)
        {
            Logger.Error($"Could not load managed package-manager configuration from {path}");
            Logger.Error(ex);
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static ManagedPackageManagerPolicy ReadWindowsPolicy()
    {
        try
        {
            using RegistryKey? key = Registry.LocalMachine.OpenSubKey(PolicyRegistryPath);
            if (key is null)
                return new();

            return new ManagedPackageManagerPolicy
            {
                ConfigurationFile = key.GetValue("ConfigurationFile") as string,
                MaximumConcurrentUninstalls = key.GetValue("MaximumConcurrentUninstalls") as int?,
                WinGetEnabled = ReadPolicyBoolean(key, "EnableWinGet"),
                PowerShellEnabled = ReadPolicyBoolean(key, "EnablePowerShell"),
                PowerShell7Enabled = ReadPolicyBoolean(key, "EnablePowerShell7"),
                WinGetExecutablePath = key.GetValue("WinGetExecutablePath") as string,
                PowerShellExecutablePath = key.GetValue("PowerShellExecutablePath") as string,
                PowerShell7ExecutablePath = key.GetValue("PowerShell7ExecutablePath") as string,
                WinGetAllowedSources = ReadPolicyStrings(key, "AllowedWinGetSources"),
                PowerShellAllowedSources = ReadPolicyStrings(key, "AllowedPowerShellSources"),
                PowerShell7AllowedSources = ReadPolicyStrings(key, "AllowedPowerShell7Sources"),
            };
        }
        catch (Exception ex)
        {
            Logger.Error("Could not read managed package-manager Group Policy");
            Logger.Error(ex);
            return new();
        }
    }

    [SupportedOSPlatform("windows")]
    private static bool? ReadPolicyBoolean(RegistryKey key, string name) =>
        key.GetValue(name) is int value ? value != 0 : null;

    [SupportedOSPlatform("windows")]
    private static List<string>? ReadPolicyStrings(RegistryKey key, string name) =>
        key.GetValue(name) is string[] values
            ? values.Where(value => !string.IsNullOrWhiteSpace(value)).ToList()
            : null;
}

public sealed class ManagedPackageManagerConfigurationDocument
{
    public int SchemaVersion { get; set; } = 1;
    public int? MaximumConcurrentUninstalls { get; set; }
    public Dictionary<string, ManagedPackageManagerEntry> Managers { get; set; } = new();
}

public sealed class ManagedPackageManagerEntry
{
    public bool Enabled { get; set; } = true;
    public string? ExecutablePath { get; set; }
    public List<string>? AllowedSources { get; set; }
}

public sealed class ManagedPackageManagerPolicy
{
    public string? ConfigurationFile { get; set; }
    public int? MaximumConcurrentUninstalls { get; set; }
    public bool? WinGetEnabled { get; set; }
    public bool? PowerShellEnabled { get; set; }
    public bool? PowerShell7Enabled { get; set; }
    public string? WinGetExecutablePath { get; set; }
    public string? PowerShellExecutablePath { get; set; }
    public string? PowerShell7ExecutablePath { get; set; }
    public List<string>? WinGetAllowedSources { get; set; }
    public List<string>? PowerShellAllowedSources { get; set; }
    public List<string>? PowerShell7AllowedSources { get; set; }
}

internal sealed class ManagedPackageManagerConfigurationSnapshot
{
    private readonly ManagedPackageManagerConfigurationDocument? _document;
    private readonly ManagedPackageManagerPolicy _policy;

    public int MaximumConcurrentUninstalls
    {
        get
        {
            int value = _policy.MaximumConcurrentUninstalls
                ?? _document?.MaximumConcurrentUninstalls
                ?? ManagedPackageManagerConfiguration.DefaultMaximumConcurrentUninstalls;
            // Invalid deployed limits fail conservatively instead of enabling a larger batch.
            return value is >= 1 and <= ManagedPackageManagerConfiguration.MaximumSupportedConcurrentUninstalls
                ? value : 1;
        }
    }

    public ManagedPackageManagerConfigurationSnapshot(
        ManagedPackageManagerConfigurationDocument? document,
        ManagedPackageManagerPolicy policy)
    {
        _document = document;
        _policy = policy;
    }

    public ManagedPackageManagerEntry? FindManager(string managerName)
    {
        string? configurationName = NormalizeManagerName(managerName);
        if (configurationName is null)
            return null;

        ManagedPackageManagerEntry defaults = new();
        ManagedPackageManagerEntry fromJson = FindJsonEntry(configurationName) ?? defaults;
        return configurationName switch
        {
            "WinGet" => Merge(
                fromJson,
                _policy.WinGetEnabled,
                _policy.WinGetAllowedSources,
                _policy.WinGetExecutablePath
            ),
            "PowerShell" => Merge(
                fromJson,
                _policy.PowerShellEnabled,
                _policy.PowerShellAllowedSources,
                _policy.PowerShellExecutablePath
            ),
            "PowerShell7" => Merge(
                fromJson,
                _policy.PowerShell7Enabled,
                _policy.PowerShell7AllowedSources,
                _policy.PowerShell7ExecutablePath
            ),
            _ => null,
        };
    }

    private ManagedPackageManagerEntry? FindJsonEntry(string managerName)
    {
        if (_document is null)
            return null;

        foreach ((string name, ManagedPackageManagerEntry entry) in _document.Managers)
            if (string.Equals(name, managerName, StringComparison.OrdinalIgnoreCase))
                return entry;

        return null;
    }

    private static ManagedPackageManagerEntry Merge(
        ManagedPackageManagerEntry json,
        bool? policyEnabled,
        List<string>? policySources,
        string? policyExecutablePath) => new()
        {
            Enabled = policyEnabled ?? json.Enabled,
            AllowedSources = policySources ?? json.AllowedSources,
            ExecutablePath = policyExecutablePath ?? json.ExecutablePath,
        };

    internal static string? NormalizeManagerName(string managerName)
    {
        if (managerName.Equals("Winget", StringComparison.OrdinalIgnoreCase)
            || managerName.Equals("WinGet", StringComparison.OrdinalIgnoreCase))
            return "WinGet";
        if (managerName.Equals("PowerShell", StringComparison.OrdinalIgnoreCase))
            return "PowerShell";
        if (managerName.Equals("PowerShell7", StringComparison.OrdinalIgnoreCase))
            return "PowerShell7";
        return null;
    }
}

[JsonSourceGenerationOptions(
    AllowTrailingCommas = true,
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    WriteIndented = true)]
[JsonSerializable(typeof(ManagedPackageManagerConfigurationDocument))]
internal sealed partial class ManagedPackageManagerJsonContext : JsonSerializerContext;
