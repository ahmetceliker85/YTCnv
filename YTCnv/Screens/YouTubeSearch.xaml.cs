using System.Collections.ObjectModel;
using System.Globalization;
using YoutubeExplode;
using YoutubeExplode.Search;
#if ANDROID
using Android.Views.InputMethods;
using Microsoft.Maui.Platform;
#endif

namespace YTCnv.Screens;

public partial class YouTubeSearch : ContentPage
{
    private YoutubeClient YouTube = new YoutubeClient();

    private SettingsSave settings = SettingsSave.Instance();

    public ObservableCollection<YouTubeResult> SearchResults { get; set; } = new();

    public YouTubeSearch()
    {
        InitializeComponent();
        MainGrid.HeightRequest = (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) - 153;
        MainGrid.RowDefinitions[1].Height = new GridLength((DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) - 240);
        BindingContext = this;
        HistoryList.BindingContext = settings;
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
        SearchEntry.ReturnType = ReturnType.Search;
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
        if (SearchEntry.IsFocused)
            SearchEntry.Unfocus();

#if ANDROID
        var activity = Platform.CurrentActivity;
        var inputMethodManager = activity.GetSystemService(Android.Content.Context.InputMethodService) as InputMethodManager;

        var windowToken = activity.CurrentFocus?.WindowToken ?? activity.Window.DecorView.WindowToken;

        if (windowToken != null)
        {
            inputMethodManager?.HideSoftInputFromWindow(windowToken, HideSoftInputFlags.None);
        }
#endif

        string query = SearchEntry.Text?.Trim();

        if (!string.IsNullOrWhiteSpace(query) && !settings.SearchHistory.Contains(query))
        {
            settings.SearchHistory.Insert(0, query);
            settings.SaveExtraData();
        }
        else if (!string.IsNullOrWhiteSpace(query) && settings.SearchHistory.Contains(query))
        {
            settings.SearchHistory.Remove(query);
            settings.SearchHistory.Insert(0, query);
            settings.SaveExtraData();
        }

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
            Author = MainPage.CleanAuthor(video.Author.ChannelTitle);
            ThumbnailUrl = video.Thumbnails?.FirstOrDefault()?.Url ?? "";
            Duration = video.Duration != null ? cleanDuration((TimeSpan)video.Duration) : "Live";
            VideoId = video.Id;
        }

        private string cleanDuration(TimeSpan duration)
        {
            return duration.Hours > 0 ? duration.ToString(@"hh\:mm\:ss") : duration.ToString(@"mm\:ss");
        }
    }

    private void OnEntryFocused(object sender, FocusEventArgs e)
    {
        double targetHeight = Math.Min(settings.SearchHistory.Count * 55 + 55, 300);

        HistoryPanel.Animate("Expand",
            callback: (progress) =>
            {
                HistoryPanel.HeightRequest = progress * targetHeight;
            },
            start: 0,
            end: 1,
            length: 200,
            easing: Easing.CubicOut);
    }

    private void OnEntryUnfocused(object sender, FocusEventArgs e)
    {
        double startHeight = HistoryPanel.HeightRequest;
        double endHeight = 55;

        HistoryPanel.Animate("Collapse",
            callback: (progress) =>
            {
                HistoryPanel.HeightRequest = startHeight - (startHeight - endHeight) * progress;
            },
            start: 0,
            end: 1,
            length: 150,
            easing: Easing.CubicIn);

    }

    private void OnBackgroundTapped(object sender, TappedEventArgs e)
    {
        Console.WriteLine("SearchEntry unfocused");
        if (SearchEntry.IsFocused)
            SearchEntry.Unfocus();

#if ANDROID
        var activity = Platform.CurrentActivity;
        var inputMethodManager = activity.GetSystemService(Android.Content.Context.InputMethodService) as InputMethodManager;

        var windowToken = activity.CurrentFocus?.WindowToken ?? activity.Window.DecorView.WindowToken;

        if (windowToken != null)
        {
            inputMethodManager?.HideSoftInputFromWindow(windowToken, HideSoftInputFlags.None);
        }
#endif
    }

    private void OnHistoryItemTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is string term)
        {
            SearchEntry.Text = term;

            if (SearchEntry.IsFocused)
                SearchEntry.Unfocus();

#if ANDROID
            var activity = Platform.CurrentActivity;
            var inputMethodManager = activity.GetSystemService(Android.Content.Context.InputMethodService) as InputMethodManager;

            var windowToken = activity.CurrentFocus?.WindowToken ?? activity.Window.DecorView.WindowToken;

            if (windowToken != null)
            {
                inputMethodManager?.HideSoftInputFromWindow(windowToken, HideSoftInputFlags.None);
            }
#endif
            OnSearchClicked(sender, new EventArgs());
        }
    }

    private void OnRemoveHistoryItem(object sender, EventArgs e)
    {
        Console.WriteLine("Remove history item clicked");
        if (sender is ImageButton b && b.CommandParameter is string term)
        {
            Console.WriteLine($"Removing history item: {term}");
            settings.SearchHistory.Remove(term);
            settings.SaveExtraData();
        }

        double startHeight = HistoryPanel.HeightRequest;
        double endHeight = Math.Min(settings.SearchHistory.Count * 55 + 55, 300);

        HistoryPanel.Animate("Collapse",
            callback: (progress) =>
            {
                HistoryPanel.HeightRequest = startHeight - (startHeight - endHeight) * progress;
            },
            start: 0,
            end: 1,
            length: 150,
            easing: Easing.CubicIn);
    }

    private void CollectionViewScrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        Console.WriteLine("Collectionview scrolled");

        if (e.VerticalDelta == 0)
        {
            return;
        }

        if (SearchEntry.IsFocused)
            SearchEntry.Unfocus();

#if ANDROID
        var activity = Platform.CurrentActivity;
        var inputMethodManager = activity.GetSystemService(Android.Content.Context.InputMethodService) as InputMethodManager;

        var windowToken = activity.CurrentFocus?.WindowToken ?? activity.Window.DecorView.WindowToken;

        if (windowToken != null)
        {
            inputMethodManager?.HideSoftInputFromWindow(windowToken, HideSoftInputFlags.None);
        }
#endif
    }
}