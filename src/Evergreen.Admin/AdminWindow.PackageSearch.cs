using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Evergreen.Configuration;
using UniGetUI.Core.Tools;

namespace Evergreen.Admin;

public sealed partial class AdminWindow
{
    private async Task FindAppsAsync(CatalogDefinition library)
    {
        using var cancellation = new CancellationTokenSource();
        var dialog = new Window
        {
            Title = CoreTools.Translate("Add apps to {0}", library.Name),
            Icon = AdminBrandStyles.CreateIcon(),
            Width = 1040,
            Height = 720,
            MinWidth = 720,
            MinHeight = 520,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        var query = new TextBox { PlaceholderText = CoreTools.Translate("Search apps, publishers, or tools") };
        AutomationProperties.SetName(query, CoreTools.Translate("Search apps, publishers, or tools"));
        var manager = new ComboBox
        {
            ItemsSource = new[] { CoreTools.Translate("All package managers") }.Concat(_packageSearch.ManagerNames).ToArray(),
            SelectedIndex = 0,
            MinWidth = 190,
        };
        AutomationProperties.SetName(manager, CoreTools.Translate("Package manager filter"));
        var results = new StackPanel { Spacing = 10 };
        var selections = new List<SearchSelection>();
        var message = new TextBlock { TextWrapping = TextWrapping.Wrap };
        AutomationProperties.SetLiveSetting(message, AutomationLiveSetting.Polite);
        var add = new Button { Content = CoreTools.Translate("Add selected to library"), IsEnabled = false };
        add.Classes.Add("primary");

        void UpdateAddState() => add.IsEnabled = selections.Any(item => item.IsSelected);
        async Task SearchAsync()
        {
            string text = query.Text?.Trim() ?? "";
            if (text.Length == 0)
            {
                message.Text = CoreTools.Translate("Enter an app name, publisher, or package ID.");
                query.Focus();
                return;
            }
            query.IsEnabled = false;
            manager.IsEnabled = false;
            results.Children.Clear();
            selections.Clear();
            add.IsEnabled = false;
            message.Text = CoreTools.Translate("Searching configured package sources...");
            try
            {
                string? selectedManager = manager.SelectedIndex <= 0 ? null : manager.SelectedItem as string;
                var found = await _packageSearch.SearchAsync(text, selectedManager, cancellation.Token);
                foreach (var package in found)
                {
                    var selection = new SearchSelection(package);
                    selections.Add(selection);
                    results.Children.Add(SearchResultCard(dialog, selection, UpdateAddState));
                }
                message.Text = found.Count == 0
                    ? CoreTools.Translate("No packages were found. Try another search or use Add manually.")
                    : found.Count == 1 ? CoreTools.Translate("1 package found") : CoreTools.Translate("{0} packages found", found.Count);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                message.Text = CoreTools.Translate("Package search failed: {0}", ex.Message);
            }
            finally
            {
                query.IsEnabled = true;
                manager.IsEnabled = true;
            }
        }

        var searchButton = new Button { Content = CoreTools.Translate("Search") };
        searchButton.Classes.Add("primary");
        searchButton.Click += async (_, _) => await SearchAsync();
        query.KeyDown += async (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            e.Handled = true;
            await SearchAsync();
        };
        var searchBar = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto"), ColumnSpacing = 10 };
        searchBar.Children.Add(query);
        Grid.SetColumn(manager, 1); searchBar.Children.Add(manager);
        Grid.SetColumn(searchButton, 2); searchBar.Children.Add(searchButton);
        var header = new StackPanel { Spacing = 10 };
        header.Children.Add(new TextBlock
        {
            Text = CoreTools.Translate("Find apps for {0}", library.Name),
            FontSize = 28,
            FontWeight = FontWeight.Bold,
        });
        header.Children.Add(new TextBlock
        {
            Text = CoreTools.Translate("Search the same configured package sources as Evergreen Endpoint | APPS. Select one or more results, then add them to this library draft."),
            TextWrapping = TextWrapping.Wrap,
        });
        header.Children.Add(searchBar);
        header.Children.Add(message);
        var cancel = new Button { Content = CoreTools.Translate("Cancel") };
        cancel.Click += (_, _) => dialog.Close();
        add.Click += (_, _) =>
        {
            try
            {
                string draft = _library.Text ?? "";
                var existing = SoftwareLibrary.Parse(draft).Single(c => c.Id == library.Id).Packages
                    .Select(entry => Identity(entry.ManagerName, entry.Source, entry.Id)).ToHashSet();
                int added = 0;
                foreach (var item in selections.Where(item => item.IsSelected))
                {
                    string identity = Identity(item.Package.ManagerName, item.Package.Source, item.Package.Id);
                    if (!existing.Add(identity)) continue;
                    draft = AdminLibraryDraft.SaveApp(draft, library.Id, null, new CatalogEntry
                    {
                        Name = item.Package.Name,
                        Id = item.Package.Id,
                        Version = item.Package.Version,
                        ManagerName = item.Package.ManagerName,
                        Source = item.Package.Source,
                        Icon = string.IsNullOrEmpty(item.IconData) ? null : item.IconData,
                    });
                    added++;
                }
                if (added == 0)
                {
                    message.Text = CoreTools.Translate("The selected packages are already in this library.");
                    return;
                }
                _library.Text = draft;
                dialog.Close();
                _status.Text = added == 1
                    ? CoreTools.Translate("Added 1 app to {0}. Export the draft to publish it.", library.Name)
                    : CoreTools.Translate("Added {0} apps to {1}. Export the draft to publish them.", added, library.Name);
                FocusLibraryAction(CoreTools.Translate("Find apps"));
            }
            catch (System.Text.Json.JsonException ex) { message.Text = CoreTools.Translate(ex.Message); }
        };
        var actions = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(cancel);
        actions.Children.Add(add);
        var layout = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 14, Margin = new Thickness(24) };
        layout.Children.Add(header);
        var scroll = new ScrollViewer { Content = results };
        Grid.SetRow(scroll, 1); layout.Children.Add(scroll);
        Grid.SetRow(actions, 2); layout.Children.Add(actions);
        dialog.Content = layout;
        dialog.Closed += (_, _) => cancellation.Cancel();
        dialog.Opened += (_, _) => query.Focus();
        await dialog.ShowDialog(this);
    }

    private Control SearchResultCard(Window dialog, SearchSelection selection, Action selectionChanged)
    {
        var checkbox = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        AutomationProperties.SetName(checkbox, CoreTools.Translate("Select {0}", selection.Package.Name));
        checkbox.IsCheckedChanged += (_, _) =>
        {
            selection.IsSelected = checkbox.IsChecked == true;
            selectionChanged();
        };
        var iconHost = new ContentControl
        {
            Content = LibrarySymbol(),
            Width = 48,
            Height = 48,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _ = LoadSearchIconAsync(selection.Package.IconUri, iconHost);
        var text = new StackPanel { Spacing = 3 };
        text.Children.Add(new TextBlock { Text = selection.Package.Name, FontSize = 17, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text = selection.Package.Id, FontSize = 12, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(new TextBlock
        {
            Text = CoreTools.Translate("{0} / {1} / {2}", selection.Package.ManagerDisplayName, selection.Package.Source,
                string.IsNullOrEmpty(selection.Package.Version) ? CoreTools.Translate("Latest") : selection.Package.Version),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
        });
        var chooseLogo = new Button { Content = CoreTools.Translate("Choose logo") };
        chooseLogo.Click += async (_, _) =>
        {
            try
            {
                string? icon = await ChooseLogoAsync(dialog);
                if (icon is null) return;
                selection.IconData = icon;
                iconHost.Content = IconImage(icon, 48);
                chooseLogo.Content = CoreTools.Translate("Replace logo");
            }
            catch (System.Text.Json.JsonException ex) { _status.Text = CoreTools.Translate(ex.Message); }
        };
        ToolTip.SetTip(chooseLogo, CoreTools.Translate("Embed a custom logo in the published library. Use this when the package source has no suitable logo."));
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), ColumnSpacing = 14 };
        row.Children.Add(checkbox);
        Grid.SetColumn(iconHost, 1); row.Children.Add(iconHost);
        Grid.SetColumn(text, 2); row.Children.Add(text);
        Grid.SetColumn(chooseLogo, 3); row.Children.Add(chooseLogo);
        var card = new Border { Child = row };
        card.Classes.Add("card");
        return card;
    }

    private static async Task LoadSearchIconAsync(Uri? uri, ContentControl host)
    {
        if (uri is null || !uri.IsFile) return;
        try
        {
            var bitmap = await Task.Run(() => new Bitmap(uri.LocalPath));
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => host.Content = new Image
            {
                Source = bitmap,
                Width = 48,
                Height = 48,
                Stretch = Stretch.Uniform,
            });
        }
        catch { /* Keep the accessible package fallback when an icon cannot be decoded. */ }
    }

    private static Control IconImage(string iconData, double size)
    {
        using var stream = new MemoryStream(CatalogIconData.Decode(iconData), writable: false);
        return new Image { Source = new Bitmap(stream), Width = size, Height = size, Stretch = Stretch.Uniform };
    }

    private static async Task<string?> ChooseLogoAsync(Window owner)
    {
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = CoreTools.Translate("Choose an app logo"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType(CoreTools.Translate("PNG or JPEG image"))
                {
                    Patterns = ["*.png", "*.jpg", "*.jpeg"],
                    MimeTypes = ["image/png", "image/jpeg"],
                },
            ],
        });
        if (files.Count != 1 || files[0].TryGetLocalPath() is not { } path) return null;
        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > CatalogIconData.MaximumDecodedBytes)
            throw new System.Text.Json.JsonException("Custom app logos must be between 1 byte and 512 KiB.");
        return CatalogIconData.CreateDataUri(await File.ReadAllBytesAsync(path));
    }

    private static string Identity(string manager, string source, string id) =>
        $"{manager}\n{source}\n{id}".ToUpperInvariant();

    private sealed class SearchSelection(AdminPackageSearchResult package)
    {
        public AdminPackageSearchResult Package { get; } = package;
        public bool IsSelected { get; set; }
        public string IconData { get; set; } = "";
    }
}
