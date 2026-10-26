using Evergreen.Configuration;
using UniGetUI.PackageEngine.Classes.Serializable;
using UniGetUI.PackageEngine.Interfaces;

namespace UniGetUI.Avalonia.Models;

public static class CatalogPackageAdapter
{
    public static CatalogEntry FromPackage(IPackage package) => new()
    {
        Id = package.Id,
        Name = package.Name,
        Version = "",
        Source = package.Source.Name,
        ManagerName = package.Manager.DisplayName,
    };

    public static SerializablePackage AsSerializable(this CatalogEntry entry) => new()
    {
        Id = entry.Id,
        Name = entry.Name,
        Version = entry.Version,
        Source = entry.Source,
        ManagerName = entry.ManagerName,
    };

    public static bool Matches(this CatalogEntry entry, string id, string manager, string source) =>
        string.Equals(entry.Id, id, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.ManagerName, manager, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.Source, source, StringComparison.OrdinalIgnoreCase);

    public static bool MatchesManager(this CatalogEntry entry, IPackageManager manager) =>
        string.Equals(entry.ManagerName, manager.Id, StringComparison.OrdinalIgnoreCase)
        || string.Equals(entry.ManagerName, manager.Name, StringComparison.OrdinalIgnoreCase)
        || string.Equals(entry.ManagerName, manager.DisplayName, StringComparison.OrdinalIgnoreCase);

    public static bool Matches(this CatalogEntry entry, IPackage package) =>
        string.Equals(entry.Id, package.Id, StringComparison.OrdinalIgnoreCase)
        && string.Equals(entry.Source, package.Source.Name, StringComparison.OrdinalIgnoreCase)
        && entry.MatchesManager(package.Manager);

    public static bool MatchesInstalled(this CatalogEntry entry, IPackage package) =>
        string.Equals(entry.Id, package.Id, StringComparison.OrdinalIgnoreCase)
        && entry.MatchesManager(package.Manager)
        // Chocolatey's installed inventory assigns DefaultSource and does not retain feed provenance.
        && (string.Equals(package.Manager.Id, "chocolatey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entry.Source, package.Source.Name, StringComparison.OrdinalIgnoreCase));
}

internal static class SoftwareCatalog
{
    public static string FilePath => SoftwareLibrary.FilePath;
    internal static Task SaveAsync(string filePath, CatalogDefinition[] catalogs) => SoftwareLibrary.SaveAsync(filePath, catalogs);
    public static Task<CatalogDefinition[]> LoadAsync() => SoftwareLibrary.LoadAsync();
    internal static Task<CatalogDefinition[]> LoadAsync(string filePath) => SoftwareLibrary.LoadAsync(filePath);
    internal static CatalogDefinition[] Parse(string json) => SoftwareLibrary.Parse(json);
}
