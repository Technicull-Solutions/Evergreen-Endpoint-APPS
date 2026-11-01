using System.Text.Json;
using Evergreen.Admin;
using Evergreen.Configuration;

namespace UniGetUI.Tests;

public class AdminLibraryDraftTests
{
    [Fact]
    public void GraphicalEditsRetainOtherLibrariesAndVersionPinsAcrossJsonRoundTrips()
    {
        string draft = AdminLibraryDraft.SaveLibrary(AdminWorkspace.EmptyLibrary, null, "design", "Design tools");
        draft = AdminLibraryDraft.SaveApp(draft, "design", null, App("Editor", "Example.Editor", "2.1.0"));
        draft = AdminLibraryDraft.SaveLibrary(draft, "design", "creative", "Creative tools");
        draft = AdminLibraryDraft.SaveApp(AdminWorkspace.FormatLibrary(draft), "creative", 0, App("Renamed editor", "Example.Editor", "2.1.0"));
        var catalogs = SoftwareLibrary.Parse(draft);
        Assert.Empty(catalogs.Single(c => c.Id == "apps").Packages);
        var entry = Assert.Single(catalogs.Single(c => c.Id == "creative").Packages);
        Assert.Equal("Renamed editor", entry.Name);
        Assert.Equal("2.1.0", entry.Version);
        Assert.Equal("winget", entry.Source);
        draft = AdminLibraryDraft.RemoveApp(draft, "creative", 0);
        Assert.Empty(SoftwareLibrary.Parse(draft).Single(c => c.Id == "creative").Packages);
        draft = AdminLibraryDraft.RemoveLibrary(draft, "creative");
        Assert.Equal("apps", Assert.Single(SoftwareLibrary.Parse(draft)).Id);
    }

    [Fact]
    public void DuplicateLibraryOrAppIsRejectedWithoutChangingTheExistingDraft()
    {
        string draft = AdminLibraryDraft.SaveApp(AdminWorkspace.EmptyLibrary, "apps", null, App("Editor", "Example.Editor", ""));
        Assert.Throws<JsonException>(() => AdminLibraryDraft.SaveLibrary(draft, null, "APPS", "Duplicate"));
        Assert.Throws<JsonException>(() => AdminLibraryDraft.SaveApp(draft, "apps", null, App("Duplicate", "example.editor", "")));
        Assert.Single(Assert.Single(SoftwareLibrary.Parse(draft)).Packages);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void StaleAppIndexIsRejected(int index)
    {
        Assert.Throws<JsonException>(() => AdminLibraryDraft.RemoveApp(AdminWorkspace.EmptyLibrary, "apps", index));
        Assert.Throws<JsonException>(() => AdminLibraryDraft.SaveApp(AdminWorkspace.EmptyLibrary, "apps", index, App("Editor", "Example.Editor", "")));
    }

    [Fact]
    public void InvalidJsonCannotBeReplacedByAGraphicalEdit() =>
        Assert.Throws<JsonException>(() => AdminLibraryDraft.SaveLibrary("{broken", null, "apps", "Apps"));

    [Fact]
    public void CustomLogoRoundTripsAndInvalidImageDataIsRejected()
    {
        byte[] pngHeader = [137, 80, 78, 71, 13, 10, 26, 10];
        string icon = CatalogIconData.CreateDataUri(pngHeader);
        CatalogEntry app = new()
        {
            Name = "Editor",
            Id = "Example.Editor",
            ManagerName = "WinGet",
            Source = "winget",
            Icon = icon,
        };
        string draft = AdminLibraryDraft.SaveApp(AdminWorkspace.EmptyLibrary, "apps", null, app);
        Assert.Equal(icon, Assert.Single(Assert.Single(SoftwareLibrary.Parse(draft)).Packages).Icon);

        string invalid = draft.Replace(icon, "data:image/png;base64,AAAA", StringComparison.Ordinal);
        Assert.Throws<JsonException>(() => SoftwareLibrary.Parse(invalid));
    }

    private static CatalogEntry App(string name, string id, string version) => new()
    {
        Name = name,
        Id = id,
        Version = version,
        ManagerName = "WinGet",
        Source = "winget",
    };
}
