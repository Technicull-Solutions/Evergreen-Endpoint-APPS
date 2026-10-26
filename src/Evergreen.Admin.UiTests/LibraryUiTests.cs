using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Evergreen.Admin;
using Evergreen.Configuration;
using UniGetUI.Core.Logging;

[assembly: AvaloniaTestApplication(typeof(Evergreen.Admin.UiTests.TestAppBuilder))]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Evergreen.Admin.UiTests;

public static class TestAppBuilder
{
    static TestAppBuilder() => AppPaths.InitializePortableApplicationData(Path.Combine(AppContext.BaseDirectory, "headless-workspace"));

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<AdminApplication>().UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}

public class LibraryUiTests
{
    [AvaloniaTheory]
    [InlineData("Light", 1100, 800)]
    [InlineData("Dark", 1100, 800)]
    [InlineData("Dark", 760, 600)]
    public void AllFourViewsKeepTheSameDraftAndInvalidJsonRemainsEditable(string theme, int width, int height)
    {
        Application.Current!.RequestedThemeVariant = theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark;
        var window = new AdminWindow { Width = width, Height = height };
        window.Show();
        var appearance = window.GetVisualDescendants().OfType<ComboBox>().Single(c => AutomationProperties.GetName(c) == "Appearance");
        appearance.SelectedIndex = theme == "Light" ? 1 : 2;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(theme == "Light" ? ThemeVariant.Light : ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
        var editor = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Classes.Contains("jsonEditor"));
        string initial = editor.Text!;
        try
        {
            var modes = window.GetVisualDescendants().OfType<ListBox>().Single(l => AutomationProperties.GetName(l) == "Library view modes");
            Assert.False(editor.IsVisible);
            Assert.Equal(1, modes.SelectedIndex);
            string draft = AdminLibraryDraft.SaveLibrary(initial, null, "engineering", "Engineering");
            draft = AdminLibraryDraft.SaveApp(draft, "engineering", null, new CatalogEntry
            {
                Name = "Example Editor",
                Id = "Example.Editor",
                ManagerName = "WinGet",
                Source = "winget",
                Version = "2.1.0",
            });
            editor.Text = draft;
            for (int mode = 0; mode < 4; mode++)
            {
                modes.SelectedIndex = mode;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(draft, editor.Text);
                Assert.Equal(mode == 3, editor.IsVisible);
                Capture(window, $"{theme}-{width}-view-{mode}");
            }
            editor.Text = "{ invalid JSON";
            modes.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(3, modes.SelectedIndex);
            Assert.True(editor.IsVisible);
            Assert.Equal("{ invalid JSON", editor.Text);
            editor.Text = draft;
            modes.SelectedIndex = 1;
            Dispatcher.UIThread.RunJobs();
            var open = window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Open library Engineering, 1 app");
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Example Editor");
            Capture(window, $"{theme}-{width}-library-apps");
        }
        finally { editor.Text = initial; window.Close(); }
    }

    [AvaloniaFact]
    public void GraphicalLibraryAndAppFormsUpdateJsonAndKeepValidationErrorsInTheForm()
    {
        var window = new AdminWindow();
        window.Show();
        var editor = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Classes.Contains("jsonEditor"));
        string initial = editor.Text!;
        try
        {
            Click(window, "New library");
            var form = Assert.Single(window.OwnedWindows);
            SetField(form, "Library name", "Design tools");
            Click(form, "Save to draft");
            Assert.Single(window.OwnedWindows); // Missing ID must keep the dialog open.
            SetField(form, "Library ID", "design");
            Click(form, "Save to draft");
            Assert.Empty(window.OwnedWindows);
            Click(window, "Add app");
            form = Assert.Single(window.OwnedWindows);
            SetField(form, "App name", "Example Editor");
            SetField(form, "Package ID", "Example.Editor");
            var manager = form.GetVisualDescendants().OfType<ComboBox>().Single();
            var source = form.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == "Source");
            manager.SelectedItem = "PowerShell7";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("PSGallery", source.Text);
            source.Text = "Internal";
            manager.SelectedItem = "WinGet";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("Internal", source.Text); // A custom repository must not be rewritten.
            source.Text = "winget";
            SetField(form, "Version (optional)", "2.1.0");
            Click(form, "Save to draft");
            var library = SoftwareLibrary.Parse(editor.Text!).Single(l => l.Id == "design");
            Assert.Equal("2.1.0", Assert.Single(library.Packages).Version);
            Assert.Equal("Edit Example Editor", AutomationProperties.GetName(Assert.IsType<Button>(window.FocusManager!.GetFocusedElement())));
            Click(window, "Edit");
            form = Assert.Single(window.OwnedWindows);
            SetField(form, "App name", "Renamed Editor");
            Click(form, "Save to draft");
            library = SoftwareLibrary.Parse(editor.Text!).Single(l => l.Id == "design");
            Assert.Equal("Renamed Editor", Assert.Single(library.Packages).Name);
            Assert.Equal("2.1.0", library.Packages[0].Version);
        }
        finally
        {
            foreach (var child in window.OwnedWindows.ToArray()) child.Close();
            editor.Text = initial; window.Close();
        }
    }

    [AvaloniaFact]
    public void AnOpenFormCannotOverwriteAReplacedDraft()
    {
        var window = new AdminWindow();
        window.Show();
        var editor = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Classes.Contains("jsonEditor"));
        string initial = editor.Text!;
        try
        {
            Click(window, "New library");
            var form = Assert.Single(window.OwnedWindows);
            SetField(form, "Library name", "Stale library"); SetField(form, "Library ID", "stale");
            string replacement = AdminLibraryDraft.SaveLibrary(initial, null, "downloaded", "Downloaded library");
            editor.Text = replacement;
            Click(form, "Save to draft");
            Assert.Single(window.OwnedWindows);
            Assert.Equal(replacement, editor.Text);
            Assert.Contains(form.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.StartsWith("The draft changed", StringComparison.Ordinal) == true);
        }
        finally
        {
            foreach (var child in window.OwnedWindows.ToArray()) child.Close();
            editor.Text = initial; window.Close();
        }
    }

    [AvaloniaFact]
    public void RemovingALibraryRequiresConfirmationAndDoesNotLeaveAStaleDetailView()
    {
        var window = new AdminWindow();
        window.Show();
        var editor = window.GetVisualDescendants().OfType<TextBox>().Single(t => t.Classes.Contains("jsonEditor"));
        string initial = editor.Text!;
        try
        {
            window.GetVisualDescendants().OfType<Button>().Single(b => AutomationProperties.GetName(b) == "Open library Apps, 0 apps")
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Click(window, "Remove library");
            Click(Assert.Single(window.OwnedWindows), "Cancel");
            Assert.Single(SoftwareLibrary.Parse(editor.Text!));
            Click(window, "Remove library");
            Click(Assert.Single(window.OwnedWindows), "Continue");
            Assert.Empty(SoftwareLibrary.Parse(editor.Text!));
            Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Content as string == "New library");
        }
        finally
        {
            foreach (var child in window.OwnedWindows.ToArray()) child.Close();
            editor.Text = initial; window.Close();
        }
    }

    private static void Click(Window window, string label)
    {
        window.GetVisualDescendants().OfType<Button>().Single(b => b.Content as string == label)
            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void SetField(Window window, string name, string value) =>
        window.GetVisualDescendants().OfType<TextBox>().Single(t => AutomationProperties.GetName(t) == name).Text = value;

    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("EVERGREEN_ADMIN_UI_ARTIFACTS") is not { Length: > 0 } path) return;
        Directory.CreateDirectory(path);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(path, name + ".png"));
    }
}
