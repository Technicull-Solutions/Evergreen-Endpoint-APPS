using System.Text.Json;
using Evergreen.Configuration;

namespace Evergreen.Admin;

/// <summary>Validated graphical edits over the same document used by the JSON editor and exports.</summary>
public static class AdminLibraryDraft
{
    public static string SaveLibrary(string json, string? originalId, string id, string name)
    {
        var catalogs = SoftwareLibrary.Parse(json).ToList();
        int index = originalId is null ? -1 : FindLibrary(catalogs, originalId);
        var library = new CatalogDefinition
        {
            Id = id.Trim(),
            Name = name.Trim(),
            Packages = index < 0 ? [] : catalogs[index].Packages,
        };
        if (index < 0) catalogs.Add(library);
        else catalogs[index] = library;
        return Save(catalogs);
    }

    public static string RemoveLibrary(string json, string id)
    {
        var catalogs = SoftwareLibrary.Parse(json).ToList();
        catalogs.RemoveAt(FindLibrary(catalogs, id));
        return Save(catalogs);
    }

    public static string SaveApp(string json, string libraryId, int? originalIndex, CatalogEntry entry)
    {
        var catalogs = SoftwareLibrary.Parse(json).ToList();
        int library = FindLibrary(catalogs, libraryId);
        var apps = catalogs[library].Packages.ToList();
        var clean = new CatalogEntry
        {
            Name = entry.Name.Trim(),
            Id = entry.Id.Trim(),
            ManagerName = entry.ManagerName,
            Source = entry.Source.Trim(),
            Version = entry.Version.Trim(),
        };
        if (clean.ManagerName is not ("WinGet" or "PowerShell" or "PowerShell7"))
            throw new JsonException("Select a supported package manager.");
        if (originalIndex is { } index)
        {
            if (index < 0 || index >= apps.Count) throw new JsonException("The app is no longer in this library.");
            apps[index] = clean;
        }
        else apps.Add(clean);
        catalogs[library] = new CatalogDefinition { Id = catalogs[library].Id, Name = catalogs[library].Name, Packages = apps.ToArray() };
        return Save(catalogs);
    }

    public static string RemoveApp(string json, string libraryId, int index)
    {
        var catalogs = SoftwareLibrary.Parse(json).ToList();
        int library = FindLibrary(catalogs, libraryId);
        var apps = catalogs[library].Packages.ToList();
        if (index < 0 || index >= apps.Count) throw new JsonException("The app is no longer in this library.");
        apps.RemoveAt(index);
        catalogs[library] = new CatalogDefinition { Id = catalogs[library].Id, Name = catalogs[library].Name, Packages = apps.ToArray() };
        return Save(catalogs);
    }

    private static int FindLibrary(List<CatalogDefinition> catalogs, string id)
    {
        int index = catalogs.FindIndex(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : throw new JsonException("The library is no longer in this draft.");
    }

    private static string Save(List<CatalogDefinition> catalogs)
    {
        var typeInfo = AdminJsonContext.Default.CatalogDocument;
        var document = new CatalogDocument { Version = 1, Catalogs = catalogs.ToArray() };
        return AdminWorkspace.FormatLibrary(JsonSerializer.Serialize(document, typeInfo));
    }
}
