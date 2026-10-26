using UniGetUI.Core.Tools;

namespace UniGetUI.Avalonia.Views.DialogPages;

public sealed class TelemetryDialog : ImmersiveConfirmationDialog
{
    public TelemetryDialog()
    {
        Configure(
            CoreTools.Translate("Usage reporting unavailable"),
            new global::Avalonia.Controls.TextBlock
            {
                Text = CoreTools.Translate("No telemetry service is configured for Evergreen Endpoint | APPS. Usage reporting is disabled."),
                TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            },
            CoreTools.Translate("OK"),
            CoreTools.Translate("Close"));
        MaxWidth = 548;
        MinWidth = 420;
    }
}
