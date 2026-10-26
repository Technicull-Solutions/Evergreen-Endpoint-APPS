using Avalonia.Styling;
using Evergreen.Admin;

namespace UniGetUI.Tests;

public sealed class AdminAppearanceTests
{
    [Fact]
    public void PortableAppearanceRoundTripsWithoutChangingOtherFiles()
    {
        string directory = Path.Combine(Path.GetTempPath(), "evergreen-admin-appearance-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string draft = Path.Combine(directory, "SoftwareCatalog.json");
            File.WriteAllText(draft, AdminWorkspace.EmptyLibrary);
            Assert.Equal("System", AdminAppearance.Load(directory));
            foreach (string value in new[] { "Dark", "Light", "System" })
            {
                AdminAppearance.Save(directory, value);
                Assert.Equal(value, AdminAppearance.Load(directory));
                Assert.Equal(AdminWorkspace.EmptyLibrary, File.ReadAllText(draft));
                Assert.False(File.Exists(Path.Combine(directory, "appearance.txt.tmp")));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => AdminAppearance.Save(directory, "unknown"));
            File.WriteAllText(Path.Combine(directory, "appearance.txt"), "unknown");
            Assert.Equal("System", AdminAppearance.Load(directory));
            Assert.Equal(ThemeVariant.Default, AdminAppearance.ToThemeVariant("unknown"));
            Assert.Equal(ThemeVariant.Dark, AdminAppearance.ToThemeVariant("Dark"));
            Assert.Equal(ThemeVariant.Light, AdminAppearance.ToThemeVariant("Light"));
        }
        finally { Directory.Delete(directory, true); }
    }
}
