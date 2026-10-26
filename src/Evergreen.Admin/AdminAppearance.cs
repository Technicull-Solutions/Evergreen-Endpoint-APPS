using Avalonia.Styling;

namespace Evergreen.Admin;

/// <summary>Portable administrator preferences; never reads employee or upstream settings.</summary>
public static class AdminAppearance
{
    public static ThemeVariant ToThemeVariant(string? value) => value switch
    {
        "Light" => ThemeVariant.Light,
        "Dark" => ThemeVariant.Dark,
        _ => ThemeVariant.Default,
    };

    public static string Load(string directory)
    {
        string path = Path.Combine(directory, "appearance.txt");
        string value = File.Exists(path) ? File.ReadAllText(path).Trim() : "System";
        return value is "Light" or "Dark" ? value : "System";
    }

    public static void Save(string directory, string value)
    {
        if (value is not ("System" or "Light" or "Dark")) throw new ArgumentOutOfRangeException(nameof(value));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "appearance.txt");
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, value);
        File.Move(temporary, path, overwrite: true);
    }
}
