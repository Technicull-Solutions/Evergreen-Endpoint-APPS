using System.Xml.Linq;
using Avalonia.Media;

namespace UniGetUI.Tests;

public class NotificationThemeTests
{
    [Fact]
    public void InformationalNotificationsHaveOpaqueBackgroundsInBothThemes()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "UPSTREAM.json")))
            root = root.Parent;
        Assert.NotNull(root);

        XDocument styles = XDocument.Load(Path.Combine(root.FullName, "src",
            "UniGetUI.Avalonia", "Assets", "Styles", "Styles.Common.axaml"));
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        XElement[] backgrounds = styles.Descendants()
            .Where(element => element.Name.LocalName == "SolidColorBrush"
                && (string?)element.Attribute(xaml + "Key") == "AboutDisclaimerBackground")
            .ToArray();
        Assert.Equal(2, backgrounds.Length);
        Assert.All(backgrounds, background =>
            Assert.Equal(byte.MaxValue, Color.Parse((string)background.Attribute("Color")!).A));
    }
}
