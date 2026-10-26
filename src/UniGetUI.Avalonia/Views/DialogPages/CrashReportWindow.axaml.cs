using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using UniGetUI.Core.Logging;
using UniGetUI.Core.Tools;

namespace UniGetUI.Avalonia.Views.DialogPages;

internal sealed partial class CrashReportWindow : ImmersiveDialog
{
    private readonly string _crashReport;

    public CrashReportWindow(string crashReport)
    {
        _crashReport = crashReport;
        InitializeComponent();
        CrashReportText.Text = crashReport;
        DontSendButton.Content = CoreTools.Translate("Close");
        SendButton.Content = CoreTools.Translate("Copy report and open issues");
    }

    private async void SendReport_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            string details = DetailsBox.Text?.Trim() ?? string.Empty;
            string report = string.IsNullOrEmpty(details) ? _crashReport : $"{details}\n\n{_crashReport}";
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
                await clipboard.SetTextAsync(report);
            // Open a blank issue form: logs and identifying data are never put in a URL or uploaded.
            CoreTools.Launch("https://github.com/Technicull-Solutions/PackageManager/issues/new/choose");
        }
        catch (Exception ex)
        {
            Logger.Warn(ex);
            SendButton.Content = CoreTools.Translate("Could not copy report");
        }
    }

    private void DontSend_Click(object? sender, RoutedEventArgs e) => Close();
}
