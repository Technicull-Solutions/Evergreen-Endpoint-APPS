using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Evergreen.Configuration;
using UniGetUI.Core.Tools;

namespace Evergreen.Admin;

public sealed partial class AdminWindow
{
    private bool _sourceDirty;
    private CancellationTokenSource? _catalogRequest;

    private Control CatalogSourcePage()
    {
        var panel = new StackPanel { Margin = new Thickness(32, 24), Spacing = 16 };
        panel.Children.Add(new TextBlock { Text = CoreTools.Translate("Catalog source"), FontSize = 32, FontWeight = FontWeight.Bold });
        panel.Children.Add(new TextBlock
        {
            Text = CoreTools.Translate("Configure your environment's Gitea catalog. Fetch into a draft, review it, then export for separate deployment to Apps."),
            TextWrapping = TextWrapping.Wrap,
        });
        var fields = new WrapPanel();
        var token = new TextBox { Width = 340, PasswordChar = '\u25CF' };
        Func<CatalogSourceProfile>? readProfile = null;
        CatalogSourceProfile? savedProfile = null;
        AutomationProperties.SetName(token, CoreTools.Translate("Repository access token (session only)"));
        TextBox Field(string label, string value, bool changesDestination = false)
        {
            var input = new TextBox { Text = value, Width = 300 };
            AutomationProperties.SetName(input, CoreTools.Translate(label));
            var group = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 16, 16) };
            group.Children.Add(new TextBlock { Text = CoreTools.Translate(label) });
            group.Children.Add(input);
            fields.Children.Add(group);
            input.TextChanged += (_, _) =>
            {
                _catalogRequest?.Cancel();
                if (changesDestination) token.Text = null;
                _sourceDirty = readProfile is not null && readProfile() != savedProfile;
                MarkDirty();
            };
            return input;
        }
        var name = Field("Environment name", "");
        var server = Field("Gitea server base URL (HTTPS)", "", true);
        var owner = Field("Repository owner or organization", "", true);
        var repository = Field("Repository name", "", true);
        var revision = Field("Branch, tag, or commit ID", "main");
        var path = Field("Catalog path in repository", "software-catalog.json");
        var card = new Border { Child = fields };
        card.Classes.Add("card");
        panel.Children.Add(card);

        CatalogSourceProfile ReadProfile() => new()
        {
            Name = name.Text?.Trim() ?? "",
            ServerUrl = server.Text?.Trim() ?? "",
            Owner = owner.Text?.Trim() ?? "",
            Repository = repository.Text?.Trim() ?? "",
            Revision = revision.Text?.Trim() ?? "",
            FilePath = path.Text?.Trim() ?? "",
        };
        readProfile = ReadProfile;
        savedProfile = ReadProfile();
        var tools = new WrapPanel();
        tools.Children.Add(ActionButton(CoreTools.Translate("Open source profile"), async () =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = CoreTools.Translate("Open a catalog source profile"),
                FileTypeFilter = [JsonType],
                AllowMultiple = false,
            });
            if (files.Count != 1 || files[0].TryGetLocalPath() is not { } file) return;
            var profile = CatalogSourceProfiles.Parse(await AdminWorkspace.OpenAsync(file, CatalogSourceProfiles.MaximumDocumentBytes));
            if (_sourceDirty && !await ConfirmAsync(CoreTools.Translate("Replace the source settings? Export any changes you want to keep first."))) return;
            // Opening a profile never connects or carries authentication to another destination.
            token.Text = null;
            name.Text = profile.Name; server.Text = profile.ServerUrl; owner.Text = profile.Owner;
            repository.Text = profile.Repository; revision.Text = profile.Revision; path.Text = profile.FilePath;
            savedProfile = ReadProfile();
            _sourceDirty = false;
            MarkDirty();
            _status.Text = CoreTools.Translate("Source profile opened. Enter a token if this repository is private; no connection has been made.");
        }));
        tools.Children.Add(ActionButton(CoreTools.Translate("Export source profile"), async () =>
        {
            var profile = ReadProfile();
            profile.Validate();
            var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = CoreTools.Translate("Export catalog source settings"),
                DefaultExtension = "json",
                FileTypeChoices = [JsonType],
                SuggestedFileName = "catalog-source.json",
                ShowOverwritePrompt = true,
            });
            if (file?.TryGetLocalPath() is not { } destination) return;
            await AdminWorkspace.ExportCatalogSourceAsync(destination, profile);
            savedProfile = profile;
            _sourceDirty = ReadProfile() != savedProfile;
            MarkDirty();
            _status.Text = CoreTools.Translate("Source profile exported without credentials. Keep environment profiles in your private workspace.");
        }));
        panel.Children.Add(tools);

        var access = new StackPanel { Spacing = 8 };
        var downloadSummary = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        access.Children.Add(new TextBlock { Text = CoreTools.Translate("Repository access token (session only)") });
        access.Children.Add(token);
        access.Children.Add(new TextBlock
        {
            Text = CoreTools.Translate("Use a repository read token for private access. It is never saved or exported and is cleared when the server or repository changes. Leave blank for anonymous access."),
            TextWrapping = TextWrapping.Wrap,
        });
        var actions = new WrapPanel();
        var cancel = new Button { Content = CoreTools.Translate("Cancel request"), IsEnabled = false, Margin = new Thickness(0, 4, 8, 4) };
        cancel.Click += (_, _) => _catalogRequest?.Cancel();
        actions.Children.Add(ActionButton(CoreTools.Translate("Fetch and validate catalog"), async () =>
        {
            var profile = ReadProfile();
            profile.Validate();
            using var request = new CancellationTokenSource();
            _catalogRequest = request;
            cancel.IsEnabled = true;
            _status.Text = CoreTools.Translate("Fetching catalog. Your current draft remains available.");
            try
            {
                using var client = new CatalogRepositoryClient();
                var result = await client.FetchAsync(profile, token.Text, request.Token);
                if (!IsVisible) return;
                if (ReadProfile() != profile)
                {
                    _status.Text = CoreTools.Translate("Source settings changed during the request. Fetch again to use the new settings; the draft has not been replaced.");
                    return;
                }
                if (_library.Text != AdminWorkspace.FormatLibrary(AdminWorkspace.EmptyLibrary)
                    && !await ConfirmAsync(CoreTools.Translate("Replace the software library draft with the validated download? Export any changes you want to keep first."))) return;
                _library.Text = result.Json;
                _status.Text = CoreTools.Translate("Fetched and validated {0} libraries and {1} apps. Review Software libraries and export for deployment. SHA-256 (download fingerprint, not publisher approval): {2}",
                    result.LibraryCount, result.AppCount, result.Sha256);
                downloadSummary.Text = CoreTools.Translate("Last validated download: {0}, revision {1}. {2}", profile.Name, profile.Revision, _status.Text);
            }
            catch (OperationCanceledException)
            {
                _status.Text = CoreTools.Translate("Catalog request cancelled. Your current draft has not been replaced.");
            }
            finally { _catalogRequest = null; cancel.IsEnabled = false; }
        }, primary: true));
        actions.Children.Add(cancel);
        actions.Children.Add(ActionButton(CoreTools.Translate("Clear token"), () => { token.Text = null; return Task.CompletedTask; }));
        access.Children.Add(actions);
        access.Children.Add(downloadSummary);
        access.Children.Add(new TextBlock
        {
            Text = CoreTools.Translate("A branch can change between downloads. Use a reviewed full commit ID for repeatable deployment. Fetching does not apply endpoint policy or install apps."),
            TextWrapping = TextWrapping.Wrap,
        });
        var accessCard = new Border { Child = access };
        accessCard.Classes.Add("card");
        panel.Children.Add(accessCard);
        Closed += (_, _) => { _catalogRequest?.Cancel(); token.Text = null; };
        return new ScrollViewer { Content = panel, HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    }
}
