using System.Text.Json;
using System.Text.Json.Serialization;

namespace Evergreen.Configuration;

public sealed class CatalogDocument
{
    [JsonPropertyName("version")]
    public required int Version { get; init; }

    [JsonPropertyName("catalogs")]
    public required CatalogDefinition[] Catalogs { get; init; }
}

public sealed class CatalogDefinition
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }
    [JsonPropertyName("name")]
    public required string Name { get; init; }
    [JsonPropertyName("packages")]
    public required CatalogEntry[] Packages { get; init; }
}

public sealed class CatalogEntry
{
    [JsonPropertyName("Id")]
    public required string Id { get; init; }
    [JsonPropertyName("Name")]
    public required string Name { get; init; }
    [JsonPropertyName("Version")]
    public string Version { get; set; } = "";
    [JsonPropertyName("Source")]
    public required string Source { get; init; }
    [JsonPropertyName("ManagerName")]
    public required string ManagerName { get; init; }

}
public static class SoftwareLibrary
{
    public const int MaximumDocumentBytes = 4 * 1024 * 1024;

    public static string FilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Technicull Solutions", "Evergreen Endpoint", "Package Manager", "SoftwareCatalog.json");

    public static async Task SaveAsync(string filePath, CatalogDefinition[] catalogs)
    {
        var document = new CatalogDocument { Version = 1, Catalogs = catalogs };
        string json = JsonSerializer.Serialize(document, CatalogJsonContext.Default.CatalogDocument);
        Parse(json);
        string destination = Path.GetFullPath(filePath);
        string temporary = Path.Combine(Path.GetDirectoryName(destination)!, $".catalog-{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, json + Environment.NewLine);
            if (File.Exists(destination))
                File.Replace(temporary, destination, null);
            else
                File.Move(temporary, destination);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public static Task<CatalogDefinition[]> LoadAsync() => LoadAsync(FilePath);

    public static async Task<CatalogDefinition[]> LoadAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        if (stream.Length > MaximumDocumentBytes) throw new JsonException("The software library exceeds the 4 MiB limit.");
        using var buffer = new MemoryStream();
        byte[] chunk = new byte[8192];
        int count;
        while ((count = await stream.ReadAsync(chunk)) > 0)
        {
            if (buffer.Length + count > MaximumDocumentBytes) throw new JsonException("The software library exceeds the 4 MiB limit.");
            buffer.Write(chunk, 0, count);
        }
        return Parse(System.Text.Encoding.UTF8.GetString(buffer.ToArray()).TrimStart('\ufeff'));
    }

    public static CatalogDefinition[] Parse(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes) throw new JsonException("The software library exceeds the 4 MiB limit.");
        var document = JsonSerializer.Deserialize(json, CatalogJsonContext.Default.CatalogDocument)
            ?? throw new JsonException("The software catalog must be a versioned JSON object.");
        if (document.Version != 1)
            throw new JsonException($"Unsupported software catalog version: {document.Version}.");
        var catalogs = document.Catalogs
            ?? throw new JsonException("The software catalog requires a catalogs array.");
        var catalogIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var catalog in catalogs)
        {
            if (catalog is null || string.IsNullOrWhiteSpace(catalog.Id)
                || string.IsNullOrWhiteSpace(catalog.Name) || catalog.Packages is null)
                throw new JsonException("Every catalog requires an id, name and packages array.");
            if (!catalogIds.Add(catalog.Id))
                throw new JsonException($"Duplicate catalog identifier: {catalog.Id}.");

            var identities = new HashSet<(string, string, string)>();
            foreach (var entry in catalog.Packages)
            {
                if (entry is null || string.IsNullOrWhiteSpace(entry.Name)
                    || string.IsNullOrWhiteSpace(entry.Id) || string.IsNullOrWhiteSpace(entry.ManagerName)
                    || string.IsNullOrWhiteSpace(entry.Source) || entry.Version is null)
                    throw new JsonException("Every catalog entry requires Name, Id, ManagerName and Source, with a non-null Version.");

                if (!identities.Add((entry.Id.ToUpperInvariant(), entry.ManagerName.ToUpperInvariant(), entry.Source.ToUpperInvariant())))
                    throw new JsonException($"Duplicate package in catalog {catalog.Id}: {entry.ManagerName}/{entry.Source}/{entry.Id}.");
            }
        }
        return catalogs;
    }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(CatalogDocument))]
internal sealed partial class CatalogJsonContext : JsonSerializerContext;
