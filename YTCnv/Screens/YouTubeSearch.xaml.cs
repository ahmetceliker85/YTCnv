using System.Collections.ObjectModel;
using System.Globalization;
using YoutubeExplode;
using YoutubeExplode.Search;

namespace YTCnv.Screens;

public partial class YouTubeSearch : ContentPage
{
    private YoutubeClient YouTube = new YoutubeClient();

    private SettingsSave settings = SettingsSave.Instance();

    public ObservableCollection<YouTubeResult> SearchResults { get; set; } = new();

    public YouTubeSearch()
    {
        InitializeComponent();
        BindingContext = this;
    }

    private async void GoBack(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("///MainPage");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        SearchResults.Clear();
        SearchEntry.Text = "";
    }

    private async void OnCopyUrlClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string videoId)
        {
            string url = $"https://www.youtube.com/watch?v={videoId}";
            await Clipboard.SetTextAsync(url);
        }
    }

    private async void OnPlsDownloadClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is string videoId)
        {
            settings.IHaveId = true;
            settings.ID = videoId;
            await Shell.Current.GoToAsync("///MainPage");
        }
    }

    private async void OnSearchClicked(object sender, EventArgs e)
    {
        string query = SearchEntry.Text?.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            SearchResults.Clear();
            return;
        }

        SearchResults.Clear();

        List<TempYouTubeResult> tempResults = new List<TempYouTubeResult>();

        GettingVidsIndicator.IsVisible = true;

        ResultsView.HeightRequest = (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) - 240;
        try
        {
            int maxResults = 10;
            int count = 0;

            await foreach (var result in YouTube.Search.GetResultsAsync(query))
            {
                if (result is VideoSearchResult video)
                {
                    tempResults.Add(new TempYouTubeResult(video));
                    count++;

                    if (count >= maxResults)
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Search failed: {ex.Message}");
            GettingVidsIndicator.IsVisible = false;
        }
        GettingVidsIndicator.IsVisible = false;
        foreach (var result in tempResults)
        {
            SearchResults.Add(new YouTubeResult(result.Title, result.VideoId, result.ThumbnailUrl, result.Author, result.Duration));
        }
    }

    public class YouTubeResult
    {
        public string Title { get; set; }
        public string VideoId { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Author { get; set; }
        public string Duration { get; set; }

        public YouTubeResult(string title, string videoID, string thumbnailUrl, string author, string duration)
        {
            Title = title;
            VideoId = videoID;
            ThumbnailUrl = thumbnailUrl;
            Author = author;
            Duration = duration;
        }
    }
    public class TempYouTubeResult
    {
        public string Title { get; set; }
        public string VideoId { get; set; }
        public string ThumbnailUrl { get; set; }
        public string Author { get; set; }
        public string Duration { get; set; }

        public TempYouTubeResult(VideoSearchResult video)
        {
            Title = video.Title;
            Author = video.Author.ChannelTitle.Replace(" - Topic", "", true, CultureInfo.InvariantCulture);
            ThumbnailUrl = video.Thumbnails?.FirstOrDefault()?.Url ?? "";
            Duration = video.Duration != null ? cleanDuration((TimeSpan)video.Duration) : "Live";
            VideoId = video.Id;
        }

        private string cleanDuration(TimeSpan duration)
        {
            return duration.Hours > 0 ? duration.ToString(@"hh\:mm\:ss") : duration.ToString(@"mm\:ss");
        }
    }
}