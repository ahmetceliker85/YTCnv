using CommunityToolkit.Maui.Views;

namespace YTCnv;

public partial class UpdatePopup : Popup
{
    public bool DontShowAgain { get; private set; } = false;

    public UpdatePopup(string latestVersion)
    {
        InitializeComponent();
        this.Color = Colors.Transparent;
        VersionLabel.Text = $"A new version ({latestVersion}) is available.";
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

    protected override Task OnDismissedByTappingOutsideOfPopup(CancellationToken token = default)
    {
        Close(DontShowAgain);
        return base.OnDismissedByTappingOutsideOfPopup(token);
    }

    private void OnCheckboxChanged(object sender, CheckedChangedEventArgs e)
    {
        DontShowAgain = e.Value;
    }
}