using CommunityToolkit.Maui.Views;
using Newtonsoft.Json;

namespace YTCnv;

public class UpdateChecker
{
    private const string GitHubApiReleases = "https://api.github.com/repos/PGAxis/YTCnv/releases/latest";

    private static SettingsSave settings = SettingsSave.Instance();

    public static async Task CheckForUpdatesAsync()
    {
        try
        {
            if (settings.DontShowUpdate)
                return;

            using HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "YTCnv-App");

            string json = await client.GetStringAsync(GitHubApiReleases);

            GitHubRelease? release = JsonConvert.DeserializeObject<GitHubRelease>(json);

            if (release == null) return;

            string latestVersion = release.tag_name.TrimStart('v');
            string currentVersion = AppInfo.Current.VersionString;

            if (Version.TryParse(latestVersion, out Version latest) &&
                Version.TryParse(currentVersion, out Version current) &&
                latest > current)
            {
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var popup = new UpdatePopup(latestVersion, release.body);
                    bool? dontShow = (bool?)await Application.Current.MainPage.ShowPopupAsync(popup);

                    if (dontShow != null)
                    {
                        if ((bool)dontShow)
                            settings.DontShowUpdate = true;
                    }
                });
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Update check failed: {ex.Message}");
        }
    }

    private class GitHubRelease
    {
        public string? tag_name { get; set; } = string.Empty;
        public string? body { get; set; } = string.Empty;
    }
}

