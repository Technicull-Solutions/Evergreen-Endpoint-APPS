using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Evergreen.Configuration;

/// <summary>Environment-owned connection settings. Credentials are deliberately absent.</summary>
public sealed record CatalogSourceProfile
{
    [JsonRequired]
    public int SchemaVersion { get; init; } = 1;
    [JsonRequired]
    public string Provider { get; init; } = "gitea";
    public required string Name { get; init; }
    public required string ServerUrl { get; init; }
    public required string Owner { get; init; }
    public required string Repository { get; init; }
    public string Revision { get; init; } = "main";
    public string FilePath { get; init; } = "software-catalog.json";

    public Uri GetDownloadUri()
    {
        Validate();
        string file = string.Join('/', FilePath.Split('/').Select(Uri.EscapeDataString));
        return new Uri(new Uri(ServerUrl.TrimEnd('/') + "/"),
            $"api/v1/repos/{Uri.EscapeDataString(Owner)}/{Uri.EscapeDataString(Repository)}/raw/{file}?ref={Uri.EscapeDataString(Revision)}");
    }

    public void Validate()
    {
        if (SchemaVersion != 1 || Provider != "gitea")
            throw new JsonException("A version 1 Gitea catalog source profile is required.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 200 || Name.Any(char.IsControl))
            throw new JsonException("Enter an environment name of at most 200 characters.");
        if (ServerUrl is null || ServerUrl.Length > 2000
            || !Uri.TryCreate(ServerUrl, UriKind.Absolute, out var server) || server.Scheme != Uri.UriSchemeHttps
            || server.UserInfo.Length != 0 || server.Query.Length != 0 || server.Fragment.Length != 0
            || ServerUrl.Any(char.IsWhiteSpace) || ServerUrl.Contains('%') || ServerUrl.Contains('\\')
            || ServerUrl.Split('/').Any(part => part is "." or ".."))
            throw new JsonException("Enter the HTTPS Gitea server base URL without credentials, a query, or a fragment.");
        ValidateSegment(Owner);
        ValidateSegment(Repository);
        if (string.IsNullOrWhiteSpace(Revision) || Revision.Length > 512 || Revision.Any(char.IsControl))
            throw new JsonException("Enter a branch, tag, or commit ID of at most 512 characters.");
        if (string.IsNullOrWhiteSpace(FilePath) || FilePath.Length > 2000)
            throw new JsonException("Enter the catalog file path relative to the repository root.");
        foreach (string part in FilePath.Split('/')) ValidateSegment(part);
    }

    private static void ValidateSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value is "." or ".." || value.Length > 512
            || value.Any(char.IsControl) || value.IndexOfAny(['/', '\\', '%', '?', '#']) >= 0)
            throw new JsonException("Repository identifiers and file path segments must be non-empty and cannot contain traversal or URL delimiters.");
    }
}

public static class CatalogSourceProfiles
{
    public const int MaximumDocumentBytes = 64 * 1024;

    public static CatalogSourceProfile Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaximumDocumentBytes)
            throw new JsonException("Catalog source profile exceeds the 64 KiB limit.");
        var profile = JsonSerializer.Deserialize(json, CatalogSourceJsonContext.Default.CatalogSourceProfile)
            ?? throw new JsonException("A catalog source profile is required.");
        profile.Validate();
        return profile;
    }

    public static string Serialize(CatalogSourceProfile profile)
    {
        profile.Validate();
        return JsonSerializer.Serialize(profile, CatalogSourceJsonContext.Default.CatalogSourceProfile);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(CatalogSourceProfile))]
internal sealed partial class CatalogSourceJsonContext : JsonSerializerContext;
