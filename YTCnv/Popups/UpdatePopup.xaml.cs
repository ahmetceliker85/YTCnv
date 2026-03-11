using CommunityToolkit.Maui.Views;

namespace YTCnv;

public partial class UpdatePopup : Popup
{
    public static bool DontShowAgain { get; private set; } = false;
    private static string latestRelease = "";
    private static SettingsSave settings = SettingsSave.Instance();

    public UpdatePopup(string latestVersion, string? changeLog)
    {
        InitializeComponent();
        this.BackgroundColor = Colors.Transparent;
        this.Padding = 0;
        latestRelease = latestVersion;
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

    private async void OnOpenLinkClicked(object sender, EventArgs e)
    {
        await Launcher.OpenAsync($"https://github.com/PGAxis/YTCnv/releases/latest");
        settings.DontShowUpdate = DontShowAgain;
        await CloseAsync();
    }

    private async void OnCancelClicked(object sender, EventArgs e)
    {
        settings.DontShowUpdate = DontShowAgain;
        await CloseAsync();
    }

    private void OnCheckboxChanged(object sender, CheckedChangedEventArgs e)
    {
        DontShowAgain = e.Value;
    }
}