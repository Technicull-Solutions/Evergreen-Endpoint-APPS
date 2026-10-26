using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using UniGetUI.Core.Logging;

namespace Evergreen.Admin;

internal static class Program
{
    internal static string? StartupError { get; private set; }
    internal static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "AdminData");
    internal static string Appearance { get; private set; } = "System";

    [STAThread]
    public static int Main(string[] args)
    {
        // Exercise the shipping source-generated document path without opening a window.
        if (args is ["--self-test", string directory])
        {
            try
            {
                string dataDirectory = Path.Combine(directory, "AdminData");
                AppPaths.InitializePortableApplicationData(dataDirectory);
                if (!string.Equals(UniGetUI.Core.Data.CoreData.UniGetUIDataDirectory,
                    Path.GetFullPath(dataDirectory), StringComparison.OrdinalIgnoreCase)) return 3;
                foreach (string appearance in new[] { "System", "Light", "Dark" })
                {
                    AdminAppearance.Save(dataDirectory, appearance);
                    if (AdminAppearance.Load(dataDirectory) != appearance) return 4;
                }
                AdminAppearance.Save(dataDirectory, "System");
                CatalogSourceSelfTest.RunAsync(directory).GetAwaiter().GetResult();
                string library = AdminWorkspace.AddApplication(AdminWorkspace.EmptyLibrary,
                    "apps", "Apps", "Example", "Example.App", "WinGet", "winget");
                string path = Path.Combine(directory, "SoftwareCatalog.json");
                AdminWorkspace.ExportAsync(path, library, true).GetAwaiter().GetResult();
                AdminWorkspace.FormatLibrary(AdminWorkspace.OpenAsync(path).GetAwaiter().GetResult());
                string graphical = AdminLibraryDraft.SaveLibrary(library, "apps", "managed", "Managed apps");
                graphical = AdminLibraryDraft.SaveApp(graphical, "managed", 0, new Evergreen.Configuration.CatalogEntry
                {
                    Name = "Example renamed",
                    Id = "Example.App",
                    ManagerName = "WinGet",
                    Source = "winget",
                    Version = "2.1.0",
                });
                AdminWorkspace.ExportAsync(path, graphical, true).GetAwaiter().GetResult();
                var graphicalResult = Evergreen.Configuration.SoftwareLibrary.Parse(AdminWorkspace.OpenAsync(path).GetAwaiter().GetResult());
                if (graphicalResult[0].Packages[0].Version != "2.1.0") return 6;
                AdminLibraryDraft.RemoveApp(graphical, "managed", 0);
                string managerPath = Path.Combine(directory, "package-managers.json");
                string managerDraft = AdminWorkspace.SetUninstallLimit(AdminWorkspace.DefaultManagers, 2);
                AdminWorkspace.ExportAsync(managerPath, managerDraft, false).GetAwaiter().GetResult();
                if (AdminWorkspace.GetUninstallLimit(AdminWorkspace.OpenAsync(managerPath).GetAwaiter().GetResult()) != 2) return 5;
                try { AdminWorkspace.FormatLibrary("{\"version\":99,\"catalogs\":[]}"); return 2; }
                catch (System.Text.Json.JsonException) { }
                return 0;
            }
            catch { return 1; }
        }
        try
        {
            AppPaths.InitializePortableApplicationData(DataDirectory);
            Appearance = AdminAppearance.Load(DataDirectory);
        }
        catch (Exception ex)
        {
            // Localization itself reads settings. Show this bootstrap error without initializing
            // the shared settings/translation engine or falling back to another product's state.
            StartupError = "The portable administrator folder is not writable. Extract it to a writable location and try again.\n\n" + ex.Message;
        }
        AppBuilder.Configure<AdminApplication>().UsePlatformDetect().StartWithClassicDesktopLifetime(args);
        return 0;
    }
}

public sealed class AdminApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        Styles.Add(new AdminBrandStyles());
        RequestedThemeVariant = AdminAppearance.ToThemeVariant(Program.Appearance);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = Program.StartupError is { } error
                ? new Window
                {
                    Title = "Evergreen Endpoint Admin",
                    Icon = AdminBrandStyles.CreateIcon(),
                    Width = 540,
                    Height = 240,
                    Content = new TextBlock
                    {
                        Text = error,
                        TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
                        Margin = new Thickness(24)
                    }
                }
                : new AdminWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
