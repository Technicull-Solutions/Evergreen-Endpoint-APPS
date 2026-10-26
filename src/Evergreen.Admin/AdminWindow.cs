using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Evergreen.Configuration;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.Core.Tools;

namespace Evergreen.Admin;

public sealed partial class AdminWindow : Window
{
    private readonly TextBox _library = Editor(AdminWorkspace.FormatLibrary(AdminWorkspace.EmptyLibrary));
    private readonly TextBox _managers = Editor(AdminWorkspace.FormatManagers(AdminWorkspace.DefaultManagers));
    private readonly TextBlock _status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _diagnostics = new() { TextWrapping = TextWrapping.Wrap };
    private string _savedLibrary = AdminWorkspace.FormatLibrary(AdminWorkspace.EmptyLibrary);
    private string _savedManagers = AdminWorkspace.FormatManagers(AdminWorkspace.DefaultManagers);
    private bool _dirty;
    private bool _confirmedClose;

    public AdminWindow()
    {
        Title = CoreTools.Translate("Evergreen Endpoint | ADMIN");
        Icon = AdminBrandStyles.CreateIcon();
        Width = 1100; Height = 800; MinWidth = 760; MinHeight = 600;
        var header = new StackPanel { Margin = new Thickness(20, 8) };
        var identity = new WrapPanel { Orientation = Orientation.Horizontal };
        using (var stream = AssetLoader.Open(new Uri("avares://Evergreen.Admin/Assets/admin.png")))
            identity.Children.Add(new Image { Source = new Bitmap(stream), Width = 40, Height = 40, Margin = new Thickness(0, 0, 12, 0) });
        var brandFont = new FontFamily("avares://Evergreen.Admin/Assets/Fonts#Lato");
        var wordmark = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, VerticalAlignment = VerticalAlignment.Center };
        wordmark.Children.Add(new TextBlock
        {
            Text = CoreTools.Translate("EVERGREEN\nENDPOINT"),
            FontFamily = brandFont,
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var separator = new Border { Width = 2, Height = 34 };
        separator.Bind(Border.BackgroundProperty, separator.GetResourceObservable("AdminGold"));
        wordmark.Children.Add(separator);
        wordmark.Children.Add(new TextBlock
        {
            Text = CoreTools.Translate("ADMIN"),
            FontFamily = brandFont,
            FontSize = 24,
            VerticalAlignment = VerticalAlignment.Center,
        });
        identity.Children.Add(wordmark);
        header.Children.Add(identity);
        var appearanceRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var appearanceLabel = new TextBlock { Text = CoreTools.Translate("Appearance"), VerticalAlignment = VerticalAlignment.Center };
        var appearance = new ComboBox
        {
            ItemsSource = new[] { CoreTools.Translate("System"), CoreTools.Translate("Light"), CoreTools.Translate("Dark") },
            SelectedIndex = Program.Appearance switch { "Light" => 1, "Dark" => 2, _ => 0 },
            MinWidth = 140,
        };
        AutomationProperties.SetName(appearance, CoreTools.Translate("Appearance"));
        appearance.SelectionChanged += (_, _) =>
        {
            string value = appearance.SelectedIndex switch { 1 => "Light", 2 => "Dark", _ => "System" };
            if (Application.Current is { } app) app.RequestedThemeVariant = AdminAppearance.ToThemeVariant(value);
            try { AdminAppearance.Save(Program.DataDirectory, value); }
            catch (Exception ex) { _status.Text = CoreTools.Translate("Appearance changed but could not be saved: {0}", ex.Message); }
        };
        appearanceRow.Children.Add(appearanceLabel);
        appearanceRow.Children.Add(appearance);
        var pages = new (string Label, string Symbol, Control Page)[]
        {
            (CoreTools.Translate("Software libraries"), "M2,3 H8 L10,5 H18 V17 H2 Z M2,7 H18", LibraryPage()),
            (CoreTools.Translate("Catalog source"), "M10,1 A9,9 0 1,0 10,19 A9,9 0 1,0 10,1 M1,10 H19 M10,1 C5,6 5,14 10,19 C15,14 15,6 10,1", CatalogSourcePage()),
            (CoreTools.Translate("Package manager configuration"), "M3,2 V18 M10,2 V18 M17,2 V18 M0,6 H6 M7,14 H13 M14,8 H20", DocumentPage(_managers, false)),
            (CoreTools.Translate("Local diagnostics"), "M0,10 H4 L7,3 L12,17 L15,10 H20", DiagnosticsPage()),
        };
        var navigation = new ListBox { Padding = new Thickness(8, 16), BorderThickness = new Thickness(0) };
        navigation.Classes.Add("adminNavigation");
        AutomationProperties.SetName(navigation, CoreTools.Translate("Admin navigation"));
        navigation.ItemsSource = pages.Select(p =>
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*"), ColumnSpacing = 12 };
            var symbol = Symbol(p.Symbol, 22);
            symbol.Bind(Avalonia.Controls.Shapes.Path.StrokeProperty, symbol.GetResourceObservable("AdminHeaderText"));
            row.Children.Add(symbol);
            var text = new TextBlock { Text = p.Label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(text, 1); row.Children.Add(text);
            var item = new ListBoxItem { Content = row, Tag = p.Page };
            AutomationProperties.SetName(item, p.Label);
            return item;
        }).ToArray();
        var content = new ContentControl { HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        navigation.SelectionChanged += (_, _) =>
        {
            if (navigation.SelectedItem is ListBoxItem { Tag: Control selected }) content.Content = selected;
        };
        navigation.SelectedIndex = 0;
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), ColumnDefinitions = new ColumnDefinitions("220,*") };
        var brandHeader = new Border { Child = header };
        brandHeader.Classes.Add("brandHeader");
        Grid.SetColumnSpan(brandHeader, 2); layout.Children.Add(brandHeader);
        appearanceRow.Margin = new Thickness(12, 8);
        appearanceRow.HorizontalAlignment = HorizontalAlignment.Right;
        appearanceLabel.Bind(TextBlock.ForegroundProperty, appearanceLabel.GetResourceObservable("AdminHeaderText"));
        Grid.SetColumn(appearanceRow, 1); layout.Children.Add(appearanceRow);
        Grid.SetRow(navigation, 1); Grid.SetRowSpan(navigation, 2); layout.Children.Add(navigation);
        Grid.SetRow(content, 1); Grid.SetColumn(content, 1); layout.Children.Add(content);
        _status.Margin = new Thickness(20, 12);
        AutomationProperties.SetLiveSetting(_status, AutomationLiveSetting.Polite);
        Grid.SetRow(_status, 2); Grid.SetColumn(_status, 1); layout.Children.Add(_status);
        Content = layout;
        _library.TextChanged += (_, _) => MarkDirty();
        _managers.TextChanged += (_, _) => MarkDirty();
        Closing += async (_, e) =>
        {
            if (!_dirty || _confirmedClose) return;
            e.Cancel = true;
            if (await ConfirmAsync(CoreTools.Translate("Close without exporting your drafts?")))
            {
                _confirmedClose = true;
                Close();
            }
        };
    }

    private void MarkDirty()
    {
        _dirty = _library.Text != _savedLibrary || _managers.Text != _savedManagers || _sourceDirty;
        if (!_dirty) return;
        _status.Text = CoreTools.Translate("Draft changed. Export to keep your changes.");
    }

    private static TextBox Editor(string text)
    {
        // Tab moves to the next control rather than trapping keyboard users in the draft.
        var editor = new TextBox { Text = text, AcceptsReturn = true, AcceptsTab = false, TextWrapping = TextWrapping.NoWrap };
        editor.Classes.Add("jsonEditor");
        AutomationProperties.SetName(editor, CoreTools.Translate("JSON draft"));
        return editor;
    }

    private WrapPanel DocumentTools(TextBox editor, bool library)
    {
        var tools = new WrapPanel { Orientation = Orientation.Horizontal };
        tools.Children.Add(ActionButton(CoreTools.Translate("New draft"), async () =>
        {
            if (await ConfirmAsync(CoreTools.Translate("Replace this draft? Export any changes you want to keep first.")))
                editor.Text = library ? AdminWorkspace.FormatLibrary(AdminWorkspace.EmptyLibrary) : AdminWorkspace.FormatManagers(AdminWorkspace.DefaultManagers);
        }));
        tools.Children.Add(ActionButton(CoreTools.Translate("Open JSON"), async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = CoreTools.Translate("Open a draft"),
                AllowMultiple = false,
                FileTypeFilter = [JsonType],
            });
            if (files.Count != 1 || files[0].TryGetLocalPath() is not { } path) return;
            string json = await AdminWorkspace.OpenAsync(path);
            string formatted = library ? AdminWorkspace.FormatLibrary(json) : AdminWorkspace.FormatManagers(json);
            if (await ConfirmAsync(CoreTools.Translate("Replace this draft? Export any changes you want to keep first.")))
                editor.Text = formatted;
        }));
        tools.Children.Add(ActionButton(CoreTools.Translate("Validate and format"), () =>
        {
            editor.Text = library ? AdminWorkspace.FormatLibrary(editor.Text ?? "") : AdminWorkspace.FormatManagers(editor.Text ?? "");
            _status.Text = CoreTools.Translate("Document structure is valid. Publisher trust, source availability, and target policy have not been verified.");
            return Task.CompletedTask;
        }));
        tools.Children.Add(ActionButton(CoreTools.Translate("Export JSON"), async () =>
        {
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = CoreTools.Translate("Export draft to your workspace"),
                DefaultExtension = "json",
                FileTypeChoices = [JsonType],
                SuggestedFileName = library ? "SoftwareCatalog.json" : "package-managers.json",
                ShowOverwritePrompt = true,
            });
            if (file?.TryGetLocalPath() is not { } path) return;
            await AdminWorkspace.ExportAsync(path, editor.Text ?? "", library);
            if (library) _savedLibrary = editor.Text ?? "";
            else _savedManagers = editor.Text ?? "";
            _dirty = _library.Text != _savedLibrary || _managers.Text != _savedManagers || _sourceDirty;
            _status.Text = CoreTools.Translate("Exported {0}. Deploy this file separately; the endpoint has not been changed.", path);
        }, primary: true));
        return tools;
    }

    private Control DocumentPage(TextBox editor, bool library)
    {
        var top = new StackPanel { Spacing = 8 };
        top.Children.Add(DocumentTools(editor, library));
        if (!library)
        {
            top.Children.Add(new TextBlock { Text = CoreTools.Translate("Group Policy overrides machine JSON. Source names must match repositories already provisioned on the device."), TextWrapping = TextWrapping.Wrap });
            top.Children.Add(UninstallPolicyForm(editor));
        }
        var page = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*"), Margin = new Thickness(32, 24), RowSpacing = 12 };
        page.Children.Add(new TextBlock { Text = CoreTools.Translate("Package manager configuration"), FontSize = 32, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        var card = new Border { Child = top };
        card.Classes.Add("card");
        Grid.SetRow(card, 1); page.Children.Add(card); Grid.SetRow(editor, 2); page.Children.Add(editor);
        return page;
    }

    private Control UninstallPolicyForm(TextBox editor)
    {
        string label = CoreTools.Translate("Maximum concurrent uninstalls");
        var maximum = new NumericUpDown
        {
            Minimum = 1,
            Maximum = ManagedPackageManagerConfiguration.MaximumSupportedConcurrentUninstalls,
            Value = AdminWorkspace.GetUninstallLimit(editor.Text ?? ""),
            Increment = 1,
            FormatString = "0",
            Width = 120,
        };
        AutomationProperties.SetName(maximum, label);
        var panel = new StackPanel { Spacing = 6 };
        panel.Children.Add(new TextBlock { Text = label });
        var row = new WrapPanel();
        row.Children.Add(maximum);
        row.Children.Add(ActionButton(CoreTools.Translate("Set uninstall limit in draft"), () =>
        {
            if (maximum.Value is not { } value || decimal.Truncate(value) != value)
                throw new InvalidOperationException(CoreTools.Translate("Enter a whole number between 1 and 100."));
            editor.Text = AdminWorkspace.SetUninstallLimit(editor.Text ?? "", checked((int)value));
            return Task.CompletedTask;
        }));
        panel.Children.Add(row);
        panel.Children.Add(new TextBlock
        {
            Text = CoreTools.Translate("Default: 3 apps. Also limits each uninstall selection. Export and deploy this draft separately; Group Policy takes precedence."),
            TextWrapping = TextWrapping.Wrap,
        });
        editor.TextChanged += (_, _) =>
        {
            try { maximum.Value = AdminWorkspace.GetUninstallLimit(editor.Text ?? ""); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException or OverflowException)
            { /* Keep the current control value while the draft is incomplete. */ }
        };
        return panel;
    }

    private Control DiagnosticsPage()
    {
        var panel = new StackPanel { Spacing = 16, Margin = new Thickness(32, 24) };
        panel.Children.Add(new TextBlock { Text = CoreTools.Translate("Local diagnostics"), FontSize = 32, FontWeight = FontWeight.Bold, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(ActionButton(CoreTools.Translate("Refresh local configuration"), () =>
        {
            ManagedPackageManagerConfiguration.Reload();
            var rows = new List<string>
            {
                CoreTools.Translate("Read-only local configuration. No package managers or broker connections are started."),
                "", CoreTools.Translate("Software library") + ": " + SoftwareLibrary.FilePath,
                CoreTools.Translate("Library file exists") + ": " + File.Exists(SoftwareLibrary.FilePath),
                CoreTools.Translate("Machine configuration") + ": " + ManagedPackageManagerConfiguration.DefaultConfigurationPath,
                CoreTools.Translate("Maximum concurrent uninstalls") + ": " + ManagedPackageManagerConfiguration.MaximumConcurrentUninstalls,
                "HKLM\\" + ManagedPackageManagerConfiguration.PolicyRegistryPath,
            };
            foreach (string manager in new[] { "WinGet", "PowerShell", "PowerShell7" })
            {
                rows.Add($"{manager}: {CoreTools.Translate("Enabled")} = {ManagedPackageManagerConfiguration.IsManagerEnabled(manager)}; "
                    + $"{CoreTools.Translate("Executable")} = {ManagedPackageManagerConfiguration.GetExecutablePath(manager) ?? CoreTools.Translate("Automatic discovery")}");
            }
            rows.Add("");
            rows.Add(CoreTools.Translate("Unsigned drafts do not establish approval or publisher trust. Opening a workspace never applies machine configuration."));
            _diagnostics.Text = string.Join(Environment.NewLine, rows);
            return Task.CompletedTask;
        }));
        panel.Children.Add(_diagnostics);
        return new ScrollViewer { Content = panel };
    }

    private Button ActionButton(string label, Func<Task> action, bool primary = false)
    {
        var button = new Button { Content = label, Margin = new Thickness(0, 4, 8, 4) };
        if (primary) button.Classes.Add("primary");
        button.Click += async (_, _) =>
        {
            button.IsEnabled = false;
            try { await action(); }
            catch (Exception ex) { _status.Text = CoreTools.Translate("Action could not be completed: {0}", CoreTools.Translate(ex.Message)); }
            finally { button.IsEnabled = true; }
        };
        return button;
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var dialog = new Window { Title = CoreTools.Translate("Review draft changes"), Icon = AdminBrandStyles.CreateIcon(), Width = 440, SizeToContent = SizeToContent.Height, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        var cancel = new Button { Content = CoreTools.Translate("Cancel") }; cancel.Click += (_, _) => dialog.Close(false);
        var proceed = new Button { Content = CoreTools.Translate("Continue") }; proceed.Click += (_, _) => dialog.Close(true);
        proceed.Classes.Add("primary");
        dialog.Opened += (_, _) => cancel.Focus();
        buttons.Children.Add(cancel); buttons.Children.Add(proceed); panel.Children.Add(buttons); dialog.Content = panel;
        return await dialog.ShowDialog<bool>(this);
    }

    private static FilePickerFileType JsonType => new("JSON") { Patterns = ["*.json"] };
}
