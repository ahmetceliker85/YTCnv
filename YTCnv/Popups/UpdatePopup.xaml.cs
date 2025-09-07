using CommunityToolkit.Maui.Views;

namespace YTCnv;

public partial class UpdatePopup : Popup
{
    public bool DontShowAgain { get; private set; } = false;

    public UpdatePopup(string latestVersion, string? changeLog)
    {
        InitializeComponent();
        this.Color = Colors.Transparent;
        VersionLabel.Text = $"A new version ({latestVersion}) is available.";
        ChangeLog.FormattedText = string.IsNullOrWhiteSpace(changeLog) ? "No changelog available." : FormatChangelog(changeLog);
    }

    private FormattedString FormatChangelog(string text)
    {
        text = text.Replace("### Change log\r\n\r\n", "");
        text = text.Replace("- ", "• ");

        FormattedString retString = new FormattedString
        {
            Spans =
            {
                new Span { Text = "Change log\r\n", FontAttributes = FontAttributes.Bold, TextColor = Colors.White},
                new Span { Text = "\r\n", FontSize = 6},
                new Span { Text = text }
            }
        };

        return retString;
    }

    private void OnOpenLinkClicked(object sender, EventArgs e)
    {
        Launcher.OpenAsync("https://github.com/PGAxis/YTCnv/releases/latest");
        Close(DontShowAgain);
    }

    private void OnCancelClicked(object sender, EventArgs e)
    {
        Close(DontShowAgain);
    }

    private void OnCheckboxChanged(object sender, CheckedChangedEventArgs e)
    {
        DontShowAgain = e.Value;
    }
}