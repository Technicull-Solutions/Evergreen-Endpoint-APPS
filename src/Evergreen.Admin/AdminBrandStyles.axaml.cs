using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;
using Avalonia.Styling;

namespace Evergreen.Admin;

public sealed partial class AdminBrandStyles : Styles
{
    public AdminBrandStyles() => AvaloniaXamlLoader.Load(this);

    public static WindowIcon CreateIcon()
    {
        using var stream = AssetLoader.Open(new Uri("avares://Evergreen.Admin/Assets/admin.ico"));
        return new WindowIcon(stream);
    }
}
