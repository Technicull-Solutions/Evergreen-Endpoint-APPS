using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Evergreen.Configuration;
using UniGetUI.Core.SettingsEngine;

namespace Evergreen.Admin;

/// <summary>Offline document operations. Never initializes package managers or requests elevation.</summary>
public static class AdminWorkspace
{
    public const string EmptyLibrary = "{\"version\":1,\"catalogs\":[{\"id\":\"apps\",\"name\":\"Apps\",\"packages\":[]}]}";
    public const string DefaultManagers = """
        {"schemaVersion":1,"maximumConcurrentUninstalls":3,"managers":{"WinGet":{"enabled":true,"allowedSources":["winget","msstore"]},"PowerShell":{"enabled":true,"allowedSources":["PSGallery"]},"PowerShell7":{"enabled":true,"allowedSources":["PSGallery"]}}}
        """;

    public static string FormatLibrary(string json)
    {
        CatalogDefinition[] catalogs = SoftwareLibrary.Parse(json);
        return SerializeLibrary(catalogs);
    }

    private static string SerializeLibrary(CatalogDefinition[] catalogs)
    {
        JsonTypeInfo<CatalogDocument> typeInfo = AdminJsonContext.Default.CatalogDocument;
        return JsonSerializer.Serialize(new CatalogDocument { Version = 1, Catalogs = catalogs }, typeInfo);
    }

    public static string FormatManagers(string json)
    {
        CheckSize(json);
        using var raw = JsonDocument.Parse(json);
        if (!raw.RootElement.TryGetProperty("schemaVersion", out var version) || version.GetInt32() != 1
            || !raw.RootElement.TryGetProperty("managers", out var managers) || managers.ValueKind != JsonValueKind.Object)
            throw new JsonException("A version 1 manager configuration requires schemaVersion and managers.");
        JsonTypeInfo<ManagedPackageManagerConfigurationDocument> typeInfo = AdminJsonContext.Default.ManagedPackageManagerConfigurationDocument;
        var document = JsonSerializer.Deserialize(json, typeInfo)
            ?? throw new JsonException("A manager configuration is required.");
        if (document.MaximumConcurrentUninstalls is < 1 or > ManagedPackageManagerConfiguration.MaximumSupportedConcurrentUninstalls)
            throw new JsonException("Maximum concurrent uninstalls must be between 1 and 100.");
        foreach (var (name, entry) in document.Managers)
        {
            if (name is not ("WinGet" or "PowerShell" or "PowerShell7") || entry is null)
                throw new JsonException("Only WinGet, PowerShell, and PowerShell7 are supported.");
            if (entry.AllowedSources?.Any(string.IsNullOrWhiteSpace) == true)
                throw new JsonException("Allowed sources cannot contain empty names.");
            if (!string.IsNullOrEmpty(entry.ExecutablePath) && !Path.IsPathFullyQualified(entry.ExecutablePath))
                throw new JsonException("An executable path must be absolute.");
        }
        return JsonSerializer.Serialize(document, typeInfo);
    }

    public static string SetUninstallLimit(string json, int maximum)
    {
        string formatted = FormatManagers(json);
        var typeInfo = AdminJsonContext.Default.ManagedPackageManagerConfigurationDocument;
        var document = JsonSerializer.Deserialize(formatted, typeInfo)!;
        document.MaximumConcurrentUninstalls = maximum;
        return FormatManagers(JsonSerializer.Serialize(document, typeInfo));
    }

    public static int GetUninstallLimit(string json) => JsonSerializer.Deserialize(
        FormatManagers(json), AdminJsonContext.Default.ManagedPackageManagerConfigurationDocument)!
        .MaximumConcurrentUninstalls ?? ManagedPackageManagerConfiguration.DefaultMaximumConcurrentUninstalls;

    public static string AddApplication(string json, string catalogId, string catalogName,
        string name, string packageId, string manager, string source)
    {
        if (manager is not ("WinGet" or "PowerShell" or "PowerShell7"))
            throw new JsonException("Select a supported package manager.");
        var catalogs = SoftwareLibrary.Parse(json).ToList();
        var catalog = catalogs.FirstOrDefault(c => string.Equals(c.Id, catalogId.Trim(), StringComparison.OrdinalIgnoreCase));
        var entry = new CatalogEntry { Name = name.Trim(), Id = packageId.Trim(), ManagerName = manager, Source = source.Trim() };
        var replacement = new CatalogDefinition
        {
            Id = catalogId.Trim(),
            Name = catalogName.Trim(),
            Packages = [.. catalog?.Packages ?? [], entry],
        };
        if (catalog is not null) catalogs[catalogs.IndexOf(catalog)] = replacement;
        else catalogs.Add(replacement);
        return FormatLibrary(SerializeLibrary(catalogs.ToArray()));
    }

    public static async Task<string> OpenAsync(string path, int maximumBytes = SoftwareLibrary.MaximumDocumentBytes)
    {
        if (maximumBytes is <= 0 or > SoftwareLibrary.MaximumDocumentBytes) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        await using var file = File.OpenRead(path);
        if (file.Length > maximumBytes) throw new IOException("Document exceeds the supported size limit.");
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        int count;
        while ((count = await file.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + count > maximumBytes) throw new IOException("Document exceeds the supported size limit.");
            buffer.Write(chunk, 0, count);
        }
        return Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\uFEFF');
    }

    public static async Task ExportAsync(string path, string json, bool library)
    {
        string formatted = library ? FormatLibrary(json) : FormatManagers(json);
        await ExportWorkspaceAsync(path, formatted);
    }

    public static Task ExportCatalogSourceAsync(string path, CatalogSourceProfile profile) =>
        ExportWorkspaceAsync(path, CatalogSourceProfiles.Serialize(profile));

    private static async Task ExportWorkspaceAsync(string path, string formatted)
    {
        string destination = Path.GetFullPath(path);
        if (string.Equals(destination, SoftwareLibrary.FilePath, StringComparison.OrdinalIgnoreCase)
            || string.Equals(destination, ManagedPackageManagerConfiguration.DefaultConfigurationPath, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Export to a workspace first. Deploy machine configuration separately as an administrator.");
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".evergreen-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, formatted + Environment.NewLine);
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private static void CheckSize(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > SoftwareLibrary.MaximumDocumentBytes)
            throw new JsonException("Document exceeds the 4 MiB limit.");
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CatalogDocument))]
[JsonSerializable(typeof(ManagedPackageManagerConfigurationDocument))]
internal sealed partial class AdminJsonContext : JsonSerializerContext;
