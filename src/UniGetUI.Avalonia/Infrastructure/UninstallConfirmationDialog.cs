using Avalonia.Controls;
using Avalonia.Media;
using UniGetUI.Avalonia.Views.DialogPages;
using UniGetUI.Core.SettingsEngine;
using UniGetUI.Core.Tools;
using UniGetUI.PackageEngine.Interfaces;

namespace UniGetUI.Avalonia.Infrastructure;

internal static class UninstallConfirmationDialog
{
    public static Task<bool> ConfirmAsync(Window owner, IPackage package)
    {
        return ConfirmAsync(owner, [package]);
    }

    public static async Task<bool> ConfirmAsync(Window owner, IReadOnlyList<IPackage> packages)
    {
        if (packages.Count == 0)
        {
            return false;
        }

        int maximum = ManagedPackageManagerConfiguration.MaximumConcurrentUninstalls;
        bool overLimit = !ManagedPackageManagerConfiguration.IsUninstallSelectionAllowed(packages.Count);
        var messageBlock = new TextBlock
        {
            Text = overLimit
                ? CoreTools.Translate("You selected {0} apps. Your administrator allows a maximum of {1} apps to be uninstalled at once. Select fewer apps and try again.", packages.Count, maximum)
                : packages.Count == 1
                ? CoreTools.Translate("Do you really want to uninstall {0}?", packages[0].Name)
                : CoreTools.Translate(
                    "Uninstall these {0} apps? This removes them from your device.",
                    packages.Count),
            Opacity = 0.82,
            TextWrapping = TextWrapping.Wrap,
        };
        var body = new StackPanel { Spacing = 12 };
        body.Children.Add(messageBlock);

        if (packages.Count > 1)
        {
            string packageListText = string.Join(
                Environment.NewLine,
                packages
                    .OrderBy(package => package.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(package => "* " + package.Name));

            var packageListBlock = new TextBlock
            {
                Text = packageListText,
                TextWrapping = TextWrapping.Wrap,
            };

            var packageListViewer = new ScrollViewer
            {
                MaxHeight = 220,
                Content = packageListBlock,
            };
            body.Children.Add(packageListViewer);
        }

        var dialog = new ImmersiveConfirmationDialog(
            overLimit ? CoreTools.Translate("Uninstall limit reached") : CoreTools.Translate("Uninstall Apps"),
            body,
            CoreTools.Translate("Uninstall Apps"),
            overLimit ? CoreTools.Translate("Close") : CoreTools.Translate("Cancel"))
        {
            FocusPrimaryButton = false,
            MaxWidth = packages.Count == 1 ? 520 : 560,
            MaxHeight = packages.Count == 1 ? 220 : 380,
        };
        var uninstallButton = dialog.FindControl<Button>("PrimaryButton")!;
        uninstallButton.Classes.Remove("accent");
        uninstallButton.Classes.Add("warning-action");
        uninstallButton.IsEnabled = !overLimit;
        await dialog.ShowDialog(owner);
        // Re-read before dispatch so a policy reload during the prompt cannot enlarge the batch.
        return !overLimit && dialog.Result is true
            && ManagedPackageManagerConfiguration.IsUninstallSelectionAllowed(packages.Count);
    }
}
