using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Evergreen.Configuration;
using UniGetUI.Core.Tools;
using Path = Avalonia.Controls.Shapes.Path;

namespace Evergreen.Admin;

public sealed partial class AdminWindow
{
    private int _libraryView = 1;
    private bool _changingLibraryView;
    private string? _openLibraryId;
    private readonly ContentControl _libraryVisual = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock _libraryTitle = new() { FontSize = 32, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _librarySummary = new() { TextWrapping = TextWrapping.Wrap };

    private Control LibraryPage()
    {
        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), RowSpacing = 16, Margin = new Thickness(32, 24) };
        var heading = new StackPanel { Spacing = 6 };
        AutomationProperties.SetLiveSetting(_librarySummary, AutomationLiveSetting.Polite);
        heading.Children.Add(_libraryTitle); heading.Children.Add(_librarySummary);
        page.Children.Add(heading);
        var tools = new StackPanel { Spacing = 8 };
        tools.Children.Add(DocumentTools(_library, true));
        var modes = new ListBox
        {
            SelectionMode = SelectionMode.Single,
            SelectedIndex = 1,
            HorizontalAlignment = HorizontalAlignment.Right,
            ItemsPanel = new FuncTemplate<Panel?>(() => new StackPanel { Orientation = Orientation.Horizontal }),
        };
        modes.Classes.Add("viewModes");
        AutomationProperties.SetName(modes, CoreTools.Translate("Library view modes"));
        string[] labels = [CoreTools.Translate("List"), CoreTools.Translate("Cards"), CoreTools.Translate("Icons"), CoreTools.Translate("JSON")];
        string[] symbols = ["M2,3 H4 M7,3 H16 M2,9 H4 M7,9 H16 M2,15 H4 M7,15 H16", "M2,2 H16 V7 H2 Z M2,11 H16 V16 H2 Z", "M2,2 H7 V7 H2 Z M11,2 H16 V7 H11 Z M2,11 H7 V16 H2 Z M11,11 H16 V16 H11 Z", "M6,2 H4 V7 L2,9 L4,11 V16 H6 M12,2 H14 V7 L16,9 L14,11 V16 H12"];
        modes.ItemsSource = labels.Select((label, index) =>
        {
            var item = new ListBoxItem { Content = Symbol(symbols[index], 18), Padding = new Thickness(10, 8, 10, 10) };
            AutomationProperties.SetName(item, label); ToolTip.SetTip(item, label);
            return item;
        }).ToArray();
        var surface = new Panel();
        surface.Children.Add(new ScrollViewer { Content = _libraryVisual, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled });
        _library.IsVisible = false;
        surface.Children.Add(_library);
        modes.SelectionChanged += (_, _) =>
        {
            if (_changingLibraryView || modes.SelectedIndex < 0) return;
            try
            {
                // Never silently replace invalid raw JSON with the last graphical projection.
                if (modes.SelectedIndex != 3) SoftwareLibrary.Parse(_library.Text ?? "");
                _libraryView = modes.SelectedIndex;
                _library.IsVisible = _libraryView == 3;
                _libraryVisual.IsVisible = _libraryView != 3;
                RefreshLibrary();
            }
            catch (System.Text.Json.JsonException ex)
            {
                _status.Text = CoreTools.Translate("Correct the JSON before switching to a graphical view: {0}", CoreTools.Translate(ex.Message));
                _changingLibraryView = true;
                modes.SelectedIndex = _libraryView;
                _changingLibraryView = false;
                _library.Focus();
            }
        };
        tools.Children.Add(modes);
        Grid.SetRow(tools, 1); page.Children.Add(tools);
        Grid.SetRow(surface, 2); page.Children.Add(surface);
        _library.TextChanged += (_, _) => { if (_libraryView != 3) RefreshLibrary(); };
        RefreshLibrary();
        return page;
    }

    private void RefreshLibrary()
    {
        _libraryTitle.Text = CoreTools.Translate("Software libraries");
        if (_libraryView == 3)
        {
            _librarySummary.Text = CoreTools.Translate("Edit the same draft as JSON. Validate it before returning to a graphical view.");
            return;
        }
        CatalogDefinition[] catalogs = SoftwareLibrary.Parse(_library.Text ?? "");
        var library = catalogs.FirstOrDefault(c => c.Id == _openLibraryId);
        if (library is null) _openLibraryId = null;
        _libraryTitle.Text = library?.Name ?? CoreTools.Translate("Software libraries");
        _librarySummary.Text = library is null
            ? CoreTools.Translate("{0} · {1} · Workspace draft", catalogs.Length == 1 ? CoreTools.Translate("1 library") : CoreTools.Translate("{0} libraries", catalogs.Length), AppCount(catalogs.Sum(c => c.Packages.Length)))
            : CoreTools.Translate("{0} · Workspace draft", AppCount(library.Packages.Length));
        var body = new StackPanel { Spacing = 16 };
        var actions = new WrapPanel();
        if (library is null)
        {
            actions.Children.Add(ActionButton(CoreTools.Translate("New library"), () => EditLibraryAsync(null), primary: true));
            body.Children.Add(actions);
            body.Children.Add(new ItemsControl
            {
                ItemsSource = catalogs,
                ItemsPanel = GalleryPanel(),
                ItemTemplate = new FuncDataTemplate<CatalogDefinition>((item, _) => item is null ? null : LibraryTile(item)),
            });
            if (catalogs.Length == 0) body.Children.Add(EmptyLibraryText(CoreTools.Translate("Create your first library to start adding apps.")));
        }
        else
        {
            actions.Children.Add(ActionButton(CoreTools.Translate("Back to libraries"), () => { _openLibraryId = null; RefreshLibrary(); FocusLibraryAction(CoreTools.Translate("New library")); return Task.CompletedTask; }));
            actions.Children.Add(ActionButton(CoreTools.Translate("Add app"), () => EditAppAsync(library, null), primary: true));
            actions.Children.Add(ActionButton(CoreTools.Translate("Edit library"), () => EditLibraryAsync(library)));
            actions.Children.Add(ActionButton(CoreTools.Translate("Remove library"), async () =>
            {
                if (!await ConfirmAsync(CoreTools.Translate("Remove {0} and its app entries from this draft? Installed apps are unaffected.", library.Name))) return;
                string updated = AdminLibraryDraft.RemoveLibrary(_library.Text ?? "", library.Id);
                _openLibraryId = null; _library.Text = updated;
                FocusLibraryAction(CoreTools.Translate("New library"));
            }));
            body.Children.Add(actions);
            body.Children.Add(new ItemsControl
            {
                ItemsSource = library.Packages.Select((app, index) => new LibraryAppRow(library, app, index)).ToArray(),
                ItemsPanel = GalleryPanel(),
                ItemTemplate = new FuncDataTemplate<LibraryAppRow>((item, _) => item is null ? null : AppTile(item)),
            });
            if (library.Packages.Length == 0) body.Children.Add(EmptyLibraryText(CoreTools.Translate("This library is empty. Add an app to get started.")));
        }
        _libraryVisual.Content = body;
    }

    private FuncTemplate<Panel?> GalleryPanel() => new(() => _libraryView == 2
        ? new WrapPanel() : new StackPanel { Spacing = _libraryView == 0 ? 8 : 12 });

    private Control LibraryTile(CatalogDefinition library)
    {
        var button = ActionButton(library.Name, () => { _openLibraryId = library.Id; RefreshLibrary(); FocusLibraryAction(CoreTools.Translate("Add app")); return Task.CompletedTask; });
        button.Classes.Add("libraryTile");
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        button.Margin = _libraryView == 2 ? new Thickness(0, 0, 16, 16) : new Thickness(0);
        button.Padding = new Thickness(_libraryView == 0 ? 12 : 20);
        button.MinHeight = _libraryView == 0 ? 56 : 112;
        if (_libraryView == 2) button.Width = 220;
        var text = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = library.Name, FontSize = _libraryView == 0 ? 16 : 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = AppCount(library.Packages.Length), TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = library.Id, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        button.Content = TileLayout(LibrarySymbol(), text);
        if (_libraryView == 0)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,2*,*,Auto"), ColumnSpacing = 12 };
            var symbol = LibrarySymbol(); symbol.Width = 32; symbol.Height = 32;
            row.Children.Add(symbol);
            var name = new TextBlock { Text = library.Name, FontSize = 16, FontWeight = FontWeight.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(name, 1); row.Children.Add(name);
            var id = new TextBlock { Text = library.Id, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(id, 2); row.Children.Add(id);
            var count = new TextBlock { Text = AppCount(library.Packages.Length), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(count, 3); row.Children.Add(count);
            button.Content = row;
        }
        AutomationProperties.SetName(button, CoreTools.Translate("Open library {0}, {1}", library.Name, AppCount(library.Packages.Length)));
        return button;
    }

    private Control AppTile(LibraryAppRow row)
    {
        var details = new StackPanel { Spacing = 6 };
        details.Children.Add(new TextBlock { Text = row.App.Name, FontSize = _libraryView == 0 ? 16 : 20, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock { Text = row.App.Id, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        details.Children.Add(new TextBlock { Text = row.App.ManagerName + " / " + row.App.Source, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        if (!string.IsNullOrEmpty(row.App.Version)) details.Children.Add(new TextBlock { Text = CoreTools.Translate("Version: {0}", row.App.Version), TextWrapping = TextWrapping.Wrap });
        var actions = new WrapPanel();
        var edit = ActionButton(CoreTools.Translate("Edit"), () => EditAppAsync(row.Library, row.Index));
        AutomationProperties.SetName(edit, CoreTools.Translate("Edit {0}", row.App.Name));
        actions.Children.Add(edit);
        var remove = ActionButton(CoreTools.Translate("Remove"), async () =>
        {
            if (await ConfirmAsync(CoreTools.Translate("Remove {0} from this library draft? The installed app is unaffected.", row.App.Name)))
            {
                _library.Text = AdminLibraryDraft.RemoveApp(_library.Text ?? "", row.Library.Id, row.Index);
                FocusLibraryAction(CoreTools.Translate("Add app"));
            }
        });
        AutomationProperties.SetName(remove, CoreTools.Translate("Remove {0}", row.App.Name));
        actions.Children.Add(remove);
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(TileLayout(LibrarySymbol(), details)); content.Children.Add(actions);
        var card = new Border { Child = content, Margin = _libraryView == 2 ? new Thickness(0, 0, 16, 16) : new Thickness(0) };
        card.Classes.Add("card");
        if (_libraryView == 2) card.Width = 260;
        return card;
    }

    private Control TileLayout(Control symbol, Control text)
    {
        if (_libraryView == 2)
        {
            var stack = new StackPanel { Spacing = 12 };
            stack.Children.Add(symbol); stack.Children.Add(text); return stack;
        }
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 16 };
        row.Children.Add(symbol); Grid.SetColumn(text, 1); row.Children.Add(text); return row;
    }

    private static Control LibrarySymbol()
    {
        var tile = new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(8), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
        tile.Bind(Border.BackgroundProperty, tile.GetResourceObservable("AdminHeader"));
        var path = Symbol("M3,5 L9,2 L15,5 V13 L9,16 L3,13 Z M3,5 L9,8 L15,5 M9,8 V16", 26);
        path.Bind(Path.StrokeProperty, path.GetResourceObservable("AdminGold"));
        tile.Child = path; return tile;
    }

    private static Path Symbol(string geometry, double size)
    {
        var path = new Path { Data = Geometry.Parse(geometry), Width = size, Height = size, Stretch = Stretch.Uniform, StrokeThickness = 1.5, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        path.Bind(Path.StrokeProperty, path.GetResourceObservable("AdminText"));
        return path;
    }

    private static Control EmptyLibraryText(string text) => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 32), FontSize = 18 };

    private static string AppCount(int count) => count == 1 ? CoreTools.Translate("1 app") : CoreTools.Translate("{0} apps", count);

    private async Task EditLibraryAsync(CatalogDefinition? library)
    {
        var form = new StackPanel { Spacing = 12 };
        var name = DraftField(form, CoreTools.Translate("Library name"), library?.Name ?? "");
        var id = DraftField(form, CoreTools.Translate("Library ID"), library?.Id ?? "");
        string? result = await EditDraftAsync(library is null ? CoreTools.Translate("New library") : CoreTools.Translate("Edit library"), form,
            () => AdminLibraryDraft.SaveLibrary(_library.Text ?? "", library?.Id, id.Text ?? "", name.Text ?? ""));
        if (result is null) return;
        _openLibraryId = id.Text?.Trim(); _library.Text = result;
        FocusLibraryAction(CoreTools.Translate("Add app"));
    }

    private async Task EditAppAsync(CatalogDefinition library, int? index)
    {
        CatalogEntry? app = index is { } position ? library.Packages[position] : null;
        var form = new StackPanel { Spacing = 12 };
        var name = DraftField(form, CoreTools.Translate("App name"), app?.Name ?? "");
        var id = DraftField(form, CoreTools.Translate("Package ID"), app?.Id ?? "");
        var manager = new ComboBox
        {
            ItemsSource = new[] { "WinGet", "PowerShell", "PowerShell7", app?.ManagerName ?? "WinGet" }.Distinct().ToArray(),
            SelectedItem = app?.ManagerName ?? "WinGet",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        form.Children.Add(new TextBlock { Text = CoreTools.Translate("Package manager") });
        AutomationProperties.SetName(manager, CoreTools.Translate("Package manager"));
        form.Children.Add(manager);
        var source = DraftField(form, CoreTools.Translate("Source"), app?.Source ?? "winget");
        string previousManager = app?.ManagerName ?? "WinGet";
        manager.SelectionChanged += (_, _) =>
        {
            string selected = manager.SelectedItem as string ?? "";
            if (selected == previousManager) return;
            previousManager = selected;
            if (string.IsNullOrWhiteSpace(source.Text) || source.Text is "winget" or "msstore" or "PSGallery")
                source.Text = selected switch { "WinGet" => "winget", "PowerShell" or "PowerShell7" => "PSGallery", _ => source.Text };
        };
        var version = DraftField(form, CoreTools.Translate("Version (optional)"), app?.Version ?? "");
        string? result = await EditDraftAsync(app is null ? CoreTools.Translate("Add app") : CoreTools.Translate("Edit app"), form,
            () => AdminLibraryDraft.SaveApp(_library.Text ?? "", library.Id, index, new CatalogEntry
            {
                Name = name.Text ?? "",
                Id = id.Text ?? "",
                ManagerName = manager.SelectedItem as string ?? "",
                Source = source.Text ?? "",
                Version = version.Text ?? "",
            }));
        if (result is not null)
        {
            _library.Text = result;
            FocusLibraryAction(CoreTools.Translate("Edit {0}", name.Text?.Trim() ?? ""));
        }
    }

    private void FocusLibraryAction(string label) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
    {
        if (_libraryVisual.IsEffectivelyVisible)
            _libraryVisual.GetVisualDescendants().OfType<Button>()
                .FirstOrDefault(b => b.Content as string == label || AutomationProperties.GetName(b) == label)?.Focus();
    }, Avalonia.Threading.DispatcherPriority.Background);

    private static TextBox DraftField(Panel form, string label, string value)
    {
        var field = new TextBox { Text = value };
        AutomationProperties.SetName(field, label);
        form.Children.Add(new TextBlock { Text = label }); form.Children.Add(field); return field;
    }

    private async Task<string?> EditDraftAsync(string title, Control form, Func<string> save)
    {
        string original = _library.Text ?? "";
        var dialog = new Window { Title = title, Icon = AdminBrandStyles.CreateIcon(), Width = 520, SizeToContent = SizeToContent.Height, CanResize = true, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 24, FontWeight = FontWeight.Bold });
        panel.Children.Add(new ScrollViewer { Content = form, MaxHeight = 380 });
        var error = new TextBlock { TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetLiveSetting(error, AutomationLiveSetting.Assertive);
        panel.Children.Add(error);
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        var cancel = new Button { Content = CoreTools.Translate("Cancel"), Margin = new Thickness(8, 0) };
        cancel.Click += (_, _) => dialog.Close();
        var accept = new Button { Content = CoreTools.Translate("Save to draft") };
        accept.Classes.Add("primary");
        accept.Click += (_, _) =>
        {
            try
            {
                if (_library.Text != original) throw new System.Text.Json.JsonException("The draft changed while this form was open. Close the form and reopen it to review the current draft.");
                dialog.Close(save());
            }
            catch (System.Text.Json.JsonException ex) { error.Text = CoreTools.Translate(ex.Message); }
        };
        actions.Children.Add(cancel); actions.Children.Add(accept); panel.Children.Add(actions);
        dialog.Content = panel;
        dialog.Opened += (_, _) => cancel.Focus();
        return await dialog.ShowDialog<string?>(this);
    }

    private sealed record LibraryAppRow(CatalogDefinition Library, CatalogEntry App, int Index);
}
