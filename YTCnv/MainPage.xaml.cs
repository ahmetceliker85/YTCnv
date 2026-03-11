#if ANDROID
using Android.Content;
using Android.Provider;
using Android.Views.InputMethods;
using Microsoft.Maui.Platform;
#endif
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Channels;
using Settings = YTCnv.Screens.Settings;
using YTCnv.Screens;
using System.Globalization;
using System.Text.RegularExpressions;
using CommunityToolkit.Maui.Extensions;

namespace YTCnv
{
    public partial class MainPage : ContentPage
    {
        CancellationTokenSource _downloadCts;
        YoutubeClient YouTube = new YoutubeClient();
        HttpClient http = new HttpClient();

        private bool _4KChoice;
        private bool fastDwnld;

        private Dictionary<double, string> audioOptions;
        private Dictionary<int, string> videoOptions;

        private string url;

        private SettingsSave settings = SettingsSave.Instance();
        private MainPageBinding mainPage = MainPageBinding.Instance();

        private bool registered = false;

        public MainPage()
        {
            InitializeComponent();
            FormatPicker.SelectedIndex = 0;
            settings.LoadSettings();
            fastDwnld = settings.QuickDwnld;
            InitializeMainPage();
            this.BindingContext = mainPage;
            DownloadList.BindingContext = settings;
            DownloadHistoryFrame.HeightRequest = (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) - 500;
            DownloadList.HeightRequest = (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) - 550;
#if ANDROID
            FormatPicker.WidthRequest = 35;
#endif
        }

        protected override async void OnAppearing()
        {
            base.OnAppearing();
            
            await UpdateChecker.CheckForUpdatesAsync();

            if (settings.DownloadHistory != null)
            {
                if (settings.DownloadHistory.Count() != 0)
                {
                    EmptyHistory.IsVisible = false;
                    DownloadList.IsVisible = true;
                }
                else
                {
                    EmptyHistory.IsVisible = true;
                    DownloadList.IsVisible = false;
                }
            }

            fastDwnld = settings.QuickDwnld;

            if (!settings.IsDownloadRunning)
            {
                ResetMainPageState(fastDwnld, false);

                if (settings.IHaveId)
                {
                    settings.IHaveId = false;
                    mainPage.UrlEntryText = settings.ID;
                }
            }
        }

        // ---------- Load methods ----------
        private async void OnLoadClicked(object sender, EventArgs e)
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            var inputMethodManager = activity.GetSystemService(Android.Content.Context.InputMethodService) as InputMethodManager;

            var windowToken = activity.CurrentFocus?.WindowToken ?? activity.Window.DecorView.WindowToken;

            if (windowToken != null)
            {
                inputMethodManager?.HideSoftInputFromWindow(windowToken, HideSoftInputFlags.None);
            }
#endif

            mainPage.StatusLabelIsVisible = false;
            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                await LoadVideoMetadata();
            }
            else
            {
                ResetMainPageState(fastDwnld, false);
                ShowPopup("Lost connection", "Please check your internet connection");
            }
        }

        private async Task LoadVideoMetadata()
        {
            settings.IsDownloadRunning = true;

            _4KChoice = settings.Use4K;

            mainPage.LoadButtonIsEnabled = false;
            url = CleanUrl(UrlEntry.Text);

            if (string.IsNullOrWhiteSpace(url))
            {
                ShowPopup("No URL", "Please enter a YouTube URL", 2);
                ResetMainPageState(fastDwnld);
                DownloadStopped();
                return;
            }

            mainPage.DownloadIndicatorIsVisible = true;
            mainPage.StatusLabelIsVisible = true;
            mainPage.StatusLabelFormattedText = "Retrieving video metadata";

            try
            {
                Video? video = await YouTube.Videos.GetAsync(url).ConfigureAwait(false);

                if (video == null)
                {
                    ShowPopup("Invalid URL", "Please enter a valid YouTube URL", 2);
                    ResetMainPageState(fastDwnld);
                    DownloadStopped();
                    return;
                }

                if (video.Duration == null || video.Duration == TimeSpan.Zero)
                {
                    ResetMainPageState(fastDwnld);
                    ShowPopup("Live stream", "The video is a live stream, and therefore can’t be downloaded");
                    DownloadStopped();
                    return;
                }

                StreamManifest streamManifest = await YouTube.Videos.Streams.GetManifestAsync(url).ConfigureAwait(false);

                List<AudioOnlyStreamInfo> audioStreams = streamManifest.GetAudioOnlyStreams().Where(s => s.Container == Container.Mp4).OrderByDescending(s => s.Bitrate).ToList();
                audioOptions = audioStreams.GroupBy(s => (int)Math.Floor(s.Bitrate.KiloBitsPerSecond)).Select(g => g.OrderByDescending(s => s.Bitrate.KiloBitsPerSecond).First()).ToDictionary(s => s.Bitrate.KiloBitsPerSecond, s => $"{Math.Round(s.Bitrate.KiloBitsPerSecond)} kbps ({s.Size.MegaBytes:F1} MB)");

                List<VideoOnlyStreamInfo> videoStreams = _4KChoice ?
                    streamManifest.GetVideoOnlyStreams().OrderByDescending(s => s.VideoQuality.MaxHeight).ToList() :
                    streamManifest.GetVideoOnlyStreams().Where(s => s.Container == Container.Mp4 && s.VideoCodec.ToString().Contains("avc")).OrderByDescending(s => s.VideoQuality.MaxHeight).ToList();
                videoOptions = videoStreams.GroupBy(s => s.VideoQuality.MaxHeight).Select(g => g.OrderByDescending(s => s.VideoQuality.MaxHeight).First()).ToDictionary(s => s.VideoQuality.MaxHeight, s => $"{s.VideoQuality.Label} ({s.Size.MegaBytes:F1} MB)");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    mainPage.LoadButtonIsVisible = false;
                    mainPage.LoadButtonIsEnabled = true;

                    mainPage.StatusLabelIsVisible = false;

                    mainPage.DownloadIndicatorIsVisible = false;

                    mainPage.DownloadOptionsIsVisible = false;

                    mainPage.QualityPickerIsVisible = true;

                    mainPage.FormatPickerSelectedIndex = 0;

                    mainPage.QualityPickerItemsSource = audioOptions.Values.ToList();
                    mainPage.QualityPickerSelectedIndex = 0;

                    mainPage.DownloadButtonIsVisible = true;
                });

                DownloadStopped();
            }
            catch (Exception ex)
            {
                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                {
                    ResetMainPageState(fastDwnld, false);
                    ShowPopup("Lost connection", "Please connect to the internet", 2);
                }
                else if (ex.Message.Contains("403") || ex.Message.Contains("404"))
                {
                    ResetMainPageState(fastDwnld, false);
                    ShowPopup("Video unavailable", "The video is private, age-restricted, does not exist, or YouTube is just not feeling it today. Please try again", 2);
                }
                else
                {
                    ResetMainPageState(fastDwnld);
                    ShowPopup("Error", ex.Message, 2);
                }
                DownloadStopped();
            }
        }

        // --------- Download methods ----------
        private async void OnDownloadClicked(object sender, EventArgs e)
        {
#if ANDROID
            var activity = Platform.CurrentActivity;
            var inputMethodManager = activity.GetSystemService(Android.Content.Context.InputMethodService) as InputMethodManager;

            var windowToken = activity.CurrentFocus?.WindowToken ?? activity.Window.DecorView.WindowToken;

            if (windowToken != null)
            {
                inputMethodManager?.HideSoftInputFromWindow(windowToken, HideSoftInputFlags.None);
            }
#endif

            mainPage.FormatPickerIsEnabled = false;

            mainPage.QualityPickerIsEnabled = false;

            if (_downloadCts != null)
                _downloadCts.Dispose();
            _downloadCts = new CancellationTokenSource();

            mainPage.StatusLabelIsVisible = false;

            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                await DoTheThing(fastDwnld);
            }
            else
            {
                ResetMainPageState(fastDwnld, false);
                ShowPopup("Lost connection", "Please check your internet connection");
            }
        }

        private async Task DoTheThing(bool useNewUrl)
        {
            settings.IsDownloadRunning = true;

            _4KChoice = settings.Use4K;

            mainPage.DownloadButtonIsVisible = false;
            mainPage.CancelButtonIsVisible = true;

            Console.WriteLine("Download started");

            int selectedFormat = mainPage.FormatPickerSelectedIndex;

            Progress<double> progress = new Progress<double>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DwnldProgress.Progress = p;
                });
            });

            if (useNewUrl)
                url = CleanUrl(UrlEntry.Text);

            if (string.IsNullOrWhiteSpace(url))
            {
                ResetMainPageState(fastDwnld);
                ShowPopup("No URL", "Please enter a YouTube URL", 2);
                DownloadStopped();
                return;
            }

            string m4aPath = Path.Combine(FileSystem.CacheDirectory, $"audio.m4a");
            string mp4Path = Path.Combine(FileSystem.CacheDirectory, "video.mp4");
            string semiOutput = Path.Combine(FileSystem.AppDataDirectory, "semi-outputVideo.mp4");
            string semiOutputAudio = Path.Combine(FileSystem.AppDataDirectory, "semi-outputAudio.mp3");
            string imagePath = Path.Combine(FileSystem.CacheDirectory, "thumbnail.jpg");

            if (File.Exists(imagePath))
                File.Delete(imagePath);

            try
            {
                mainPage.DownloadIndicatorIsVisible = true;
                mainPage.StatusLabelIsVisible = true;
                mainPage.StatusLabelFormattedText = "Retrieving video metadata";

                Video? video = await YouTube.Videos.GetAsync(url).ConfigureAwait(false);

                if (video == null)
                {
                    ResetMainPageState(fastDwnld);
                    ShowPopup("Invalid URL", "Please enter a valid URL", 2);
                    DownloadStopped();
                    return;
                }

                if (video.Duration == null || video.Duration == TimeSpan.Zero)
                {
                    ResetMainPageState(fastDwnld);
                    ShowPopup("Live stream", "The video is a live stream, and therefore can’t be downloaded");
                    DownloadStopped();
                    return;
                }

#if ANDROID
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var context = Android.App.Application.Context;
                    var intent = new Intent(context, typeof(DownloadNotificationService));
                    context.StartForegroundService(intent);
                });
#endif

                string author = CleanAuthor(video.Author.ChannelTitle);

                string title = CleanTitle(video.Title, ref author);
                string unalteredTitle = video.Title.Trim();

                url = video.Id.ToString();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        if (!settings.DownloadHistory.Any(item => item.UrlOrID == url))
                        {
                            SettingsSave.HistoryItem newItem = new SettingsSave.HistoryItem { Title = unalteredTitle, UrlOrID = url };
                            settings.DownloadHistory.Insert(0, newItem);
                            settings.SaveExtraData();
                            EmptyHistory.IsVisible = false;
                            DownloadList.IsVisible = true;
                            DownloadList.ScrollTo(newItem, ScrollToPosition.Start);
                        }
                        else
                        {
                            settings.DownloadHistory.Remove(settings.DownloadHistory.FirstOrDefault(item => item.UrlOrID == url));
                            SettingsSave.HistoryItem newItem = new SettingsSave.HistoryItem { Title = unalteredTitle, UrlOrID = url };
                            settings.DownloadHistory.Insert(0, newItem);
                            settings.SaveExtraData();
                            EmptyHistory.IsVisible = false;
                            DownloadList.IsVisible = true;
                            DownloadList.ScrollTo(newItem, ScrollToPosition.Start);
                        }
                    }
                });

                var popup = new TitleAuthor(title, author);

                await this.ShowPopupAsync(popup);

                author = CleanAuthor(popup.resultAuthor);
                title = CleanTitle(popup.resultTitle, author);

                string thumbnailUrl = video.Thumbnails.GetWithHighestResolution().Url;
                byte[] bytes = await http.GetByteArrayAsync(thumbnailUrl);
                await File.WriteAllBytesAsync(imagePath, bytes);

#if ANDROID
                if (!registered)
                {
                    settings.ffmpegReciever.OnFFmpegFinished += result =>
                    {
                        if (result.code == 0)
                        {
                            FinishUp(result.audioVideoElse, result.fileTitle);
                        }
                        else
                        {
                            itScrewedUp();
                        }
                    };

                    registered = true;
                }
#endif

                mainPage.StatusLabelFormattedText = "Retrieving video";

                StreamManifest streamManifest = await YouTube.Videos.Streams.GetManifestAsync(url, _downloadCts.Token).ConfigureAwait(false);

                mainPage.DownloadIndicatorIsVisible = false;
                mainPage.DwnldProgressIsVisible = true;
                mainPage.StatusLabelFormattedText = new FormattedString { Spans = { new Span { Text = "Downloading " }, new Span { Text = title, FontAttributes = FontAttributes.Bold } } };

                if (File.Exists(m4aPath))
                    File.Delete(m4aPath);
                if (File.Exists(mp4Path))
                    File.Delete(mp4Path);
                if (File.Exists(semiOutput))
                    File.Delete(semiOutput);
                if (File.Exists(semiOutputAudio))
                    File.Delete(semiOutputAudio);

                if (selectedFormat == 0)
                {
                    IStreamInfo audioStream = fastDwnld ? streamManifest.GetAudioOnlyStreams().Where(s => s.Container == Container.Mp4).TryGetWithHighestBitrate() : streamManifest.GetAudioOnlyStreams().Where(s => s.Container == Container.Mp4).FirstOrDefault(s => s.Bitrate.KiloBitsPerSecond == audioOptions.ElementAt(qualityPicker.SelectedIndex).Key);
                    await YouTube.Videos.Streams.DownloadAsync(audioStream, m4aPath, progress: progress, cancellationToken: _downloadCts.Token).ConfigureAwait(false);

                    mainPage.DwnldProgressIsVisible = false;
                    mainPage.DownloadIndicatorIsVisible = true;
                    mainPage.StatusLabelFormattedText = "Adding metadata";

                    GC.AddMemoryPressure(audioStream.Size.Bytes + 3);
#if ANDROID
                    MainThread.BeginInvokeOnMainThread(() => FFmpegInterop.RunFFmpegCommand($"-y -i \"{m4aPath}\" -i \"{imagePath}\" -map 0:a -map 1:v -c:a libmp3lame -b:a 128k -c:v mjpeg -disposition:v attached_pic -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover\" -metadata title=\"{title}\" -metadata artist=\"{author}\"  -threads 1 \"{semiOutputAudio}\"", 1, title));
#endif
#if WINDOWS
                    var ffmpegArgs = $"-y -i \"{m4aPath}\" -i \"{imagePath}\" -map 0:a -map 1:v -c:a libmp3lame -b:a 128k -c:v mjpeg -disposition:v attached_pic -id3v2_version 3 -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover\" -metadata title=\"{title}\" -metadata artist=\"{author}\"  -threads 1 \"{semiOutputAudio}\"";
                    int exitCode = await FFmpegInterop.RunFFmpegCommand(ffmpegArgs, line => Console.WriteLine(line)).ConfigureAwait(false);
                    if (exitCode == 0)
                        FinishUp(1, title);
                    else
                        itScrewedUp();
#endif
                }
                if (selectedFormat == 1)
                {
                    IStreamInfo audioStream = streamManifest.GetAudioOnlyStreams().Where(s => s.Container == Container.Mp4).TryGetWithHighestBitrate();
                    IVideoStreamInfo videoStream = fastDwnld ?
                        (_4KChoice ? 
                            (IVideoStreamInfo)streamManifest.GetVideoOnlyStreams().TryGetWithHighestBitrate() :
                            (IVideoStreamInfo)streamManifest.GetVideoOnlyStreams().Where(s => s.Container == Container.Mp4 && s.VideoCodec.ToString().Contains("avc")).TryGetWithHighestBitrate()) :
                        (_4KChoice ?
                        (videoOptions.ElementAt(qualityPicker.SelectedIndex).Key > 1080 ?
                            (IVideoStreamInfo)streamManifest.GetVideoOnlyStreams().FirstOrDefault(s => s.VideoQuality.MaxHeight == videoOptions.ElementAt(qualityPicker.SelectedIndex).Key) :
                            (IVideoStreamInfo)streamManifest.GetVideoOnlyStreams().Where(s => s.Container == Container.Mp4 && s.VideoCodec.ToString().Contains("avc")).FirstOrDefault(s => s.VideoQuality.MaxHeight == videoOptions.ElementAt(qualityPicker.SelectedIndex).Key)) :
                        streamManifest.GetVideoOnlyStreams().Where(s => s.Container == Container.Mp4 && s.VideoCodec.ToString().Contains("avc")).FirstOrDefault(s => s.VideoQuality.MaxHeight == videoOptions.ElementAt(qualityPicker.SelectedIndex).Key));

                    bool isMoreThan1080p = videoStream.VideoQuality.MaxHeight > 1080;

                    Task audioTask = YouTube.Videos.Streams.DownloadAsync(audioStream, m4aPath, cancellationToken: _downloadCts.Token).AsTask();
                    Task videoTask = YouTube.Videos.Streams.DownloadAsync(videoStream, mp4Path, progress: progress, cancellationToken: _downloadCts.Token).AsTask();

                    await Task.WhenAll(audioTask, videoTask).ConfigureAwait(false);

                    mainPage.DwnldProgressIsVisible = false;
                    mainPage.DownloadIndicatorIsVisible = true;
                    mainPage.StatusLabelFormattedText = "Joining audio and video";

                    GC.AddMemoryPressure(videoStream.Size.Bytes + 3);
#if ANDROID
                    if (_4KChoice)
                    {
                        if (isMoreThan1080p)
                            MainThread.BeginInvokeOnMainThread(() => FFmpegInterop.RunFFmpegCommand($"-y -i \"{mp4Path}\" -i \"{m4aPath}\" -c:v libx264 -pix_fmt yuv420p -preset faster -crf 23 -c:a copy -map 0:v:0 -map 1:a:0 -shortest -metadata title=\"{title}\" -metadata artist=\"{author}\" \"{semiOutput}\"", 2, title));
                        else
                            MainThread.BeginInvokeOnMainThread(() => FFmpegInterop.RunFFmpegCommand($"-y -i \"{mp4Path}\" -i \"{m4aPath}\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 -shortest -metadata title=\"{title}\" -metadata artist=\"{author}\" \"{semiOutput}\"", 2, title));
                    }
                    else
                        MainThread.BeginInvokeOnMainThread(() => FFmpegInterop.RunFFmpegCommand($"-y -i \"{mp4Path}\" -i \"{m4aPath}\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 -shortest -metadata title=\"{title}\" -metadata artist=\"{author}\" \"{semiOutput}\"", 2, title));
#endif
#if WINDOWS
                    string ffmpegArgs = "";

                    if (_4KChoice)
                    {
                        if (isMoreThan1080p)
                            ffmpegArgs = $"-y -i \"{mp4Path}\" -i \"{m4aPath}\" -c:v libx264 -pix_fmt yuv420p -preset faster -crf 23 -c:a copy -map 0:v:0 -map 1:a:0 -shortest -metadata title=\"{title}\" -metadata artist=\"{author}\" \"{semiOutput}\"";
                        else
                            ffmpegArgs = $"-y -i \"{mp4Path}\" -i \"{m4aPath}\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 -shortest -metadata title=\"{title}\" -metadata artist=\"{author}\" \"{semiOutput}\"";
                    }
                    else
                        ffmpegArgs = $"-y -i \"{mp4Path}\" -i \"{m4aPath}\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 -shortest -metadata title=\"{title}\" -metadata artist=\"{author}\" \"{semiOutput}\"";

                    int exitCode = await FFmpegInterop.RunFFmpegCommand(ffmpegArgs, line => Console.WriteLine(line)).ConfigureAwait(false);
                    if (exitCode == 0)
                        FinishUp(2, title);
                    else
                        itScrewedUp();
#endif
                }
            }
            catch (OperationCanceledException)
            {
                ResetMainPageState(fastDwnld, false);
                ShowPopup("Canceled", "The download was cancelled.", 0);

                DeleteFiles();

#if ANDROID
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var context = Android.App.Application.Context;
                    var stopIntent = new Intent(context, Java.Lang.Class.FromType(typeof(DownloadNotificationService)));
                    context.StopService(stopIntent);
                });

#endif
#if ANDROID || WINDOWS
                FFmpegInterop.CancelFFmpeg();
#endif

                DownloadStopped();
            }
            catch (Exception ex)
            {
                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                {
                    ResetMainPageState(fastDwnld, false);
                    ShowPopup("Lost connection", "Please connect to the internet", 2);
                    DeleteFiles();
                }
                else if (ex.Message.Contains("403") || ex.Message.Contains("404"))
                {
                    ResetMainPageState(fastDwnld, false);
                    ShowPopup("Video unavailable", "The video is private, age-restricted, does not exist, or YouTube is just not feeling it today. Please try again", 2);
                    DeleteFiles();
                }
                else if (ex.Message.Contains("ID or URL"))
                {
                    ResetMainPageState(fastDwnld);
                    ShowPopup("Invalid URL", "Please enter a valid YouTube URL", 2);
                    DeleteFiles();
                }
                else
                {
                    Console.WriteLine(ex.Message);
                    ResetMainPageState(fastDwnld);
                    ShowPopup("Error", ex.Message, 2);
                    DeleteFiles();
                }

#if ANDROID
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var context = Android.App.Application.Context;
                    var stopIntent = new Intent(context, Java.Lang.Class.FromType(typeof(DownloadNotificationService)));
                    context.StopService(stopIntent);
                });

#endif
#if ANDROID || WINDOWS
                FFmpegInterop.CancelFFmpeg();
#endif

                DownloadStopped();
            }

            void DeleteFiles()
            {
                if (File.Exists(m4aPath))
                    File.Delete(m4aPath);
                if (File.Exists(mp4Path))
                    File.Delete(mp4Path);
                if (File.Exists(semiOutput))
                    File.Delete(semiOutput);
                if (File.Exists(semiOutputAudio))
                    File.Delete(semiOutputAudio);
                if (File.Exists(imagePath))
                    File.Delete(imagePath);
            }

            void FinishUp(byte audioVideoElse, string fileTitle)
            {
#if ANDROID
                switch (audioVideoElse)
                {
                    case 1:
                        SaveAudio(Android.App.Application.Context, fileTitle + ".mp3", semiOutputAudio);
                        break;
                    case 2:
                        SaveVideo(Android.App.Application.Context, fileTitle + ".mp4", semiOutput);
                        break;
                    default:
                        break;
                }  

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var context = Android.App.Application.Context;
                    var stopIntent = new Intent(context, Java.Lang.Class.FromType(typeof(DownloadNotificationService)));
                    context.StopService(stopIntent);
                });
#endif
#if WINDOWS
                switch (audioVideoElse)
                {
                    case 1:
                        SaveToDownloads(fileTitle + ".mp3", semiOutputAudio);
                        break;
                    case 2:
                        SaveToDownloads(fileTitle + ".mp4", semiOutput);
                        break;
                    default:
                        break;
                }
#endif
#if ANDROID || WINDOWS
                FFmpegInterop.CancelFFmpeg();
#endif

                DeleteFiles();
                DownloadStopped();

                ResetMainPageState(fastDwnld);
                ShowPopup("Finished", "The download has completed successfully.", 1);
            }

            void itScrewedUp()
            {
#if ANDROID
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var context = Android.App.Application.Context;
                    var stopIntent = new Intent(context, Java.Lang.Class.FromType(typeof(DownloadNotificationService)));
                    context.StopService(stopIntent);
                });
#endif
#if ANDROID || WINDOWS
                FFmpegInterop.CancelFFmpeg();
#endif

                DeleteFiles();
                DownloadStopped();

                ResetMainPageState(fastDwnld);
                ShowPopup("Failed", "The app failed to add metadata and save the file.", 2);
            }
        }

        // ---------- Title, author and URL cleaning methods ----------
        public static string CleanAuthor(string author)
        {
            author = Regex.Replace(author, @"(VEVO|OfficialVEVO|TV|- Topic)$", "", RegexOptions.IgnoreCase).Trim();

            return author;
        }

        public static string CleanTitle(string title, ref string author)
        {
            List<string> toRemove = new List<string> { "Official Music Video", "Official Video", "Lyric Video", "Lyrics Video", "Official Audio", "Audio", "Official Audio Visualizer", "Official Song", "Full Album", "Deluxe Edition", "Lyrics" };

            title = new string(title.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());

            foreach (string subString in toRemove)
            {
                title = title.Replace("(" + subString + ")", "", true, CultureInfo.InvariantCulture);
                title = title.Replace("[" + subString + "]", "", true, CultureInfo.InvariantCulture);
            }

            Console.WriteLine("Title is " + title);
            Console.WriteLine("Author is " + author);

            title = Regex.Replace(title, @"\[.*?\]", "");
            title = title.Replace($"{author} - ", "", true, CultureInfo.InvariantCulture);
            title = title.Replace($"{author}-", "", true, CultureInfo.InvariantCulture);
            title = title.Replace($" - {author}", "", true, CultureInfo.InvariantCulture);
            title = title.Replace($"-{author}", "", true, CultureInfo.InvariantCulture);

            string[] titleParts = title.Split(new[] { " - " }, StringSplitOptions.None);

            Console.WriteLine("Title parts are " + string.Join(" | ", titleParts));

            if (titleParts.Length > 1)
            {
                for (int i = 0; i < titleParts.Length; i++)
                {
                    string normalizedPart = titleParts[i].Replace(" ", "");
                    string normalizedAuthor = author.Replace(" ", "");

                    if (normalizedPart.Contains(normalizedAuthor, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Match found at index {i}: {titleParts[i]}");

                        author = titleParts[i];

                        var remainingParts = titleParts.Where((part, index) => index != i);
                        title = string.Join(" - ", remainingParts);

                        Console.WriteLine($"Final title: {title}, final author: {author}");

                        break;
                    }
                }
            }
            else if (titleParts.Length == 1)
            {
                titleParts = titleParts[0].Split(new[] { "-" }, StringSplitOptions.None);

                if (titleParts.Length > 1)
                {
                    for (int i = 0; i < titleParts.Length; i++)
                    {
                        string normalizedPart = titleParts[i].Replace(" ", "");
                        string normalizedAuthor = author.Replace(" ", "");

                        if (normalizedPart.Contains(normalizedAuthor, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"Match found at index {i}: {titleParts[i]}");

                            author = titleParts[i];

                            var remainingParts = titleParts.Where((part, index) => index != i);
                            title = string.Join(" - ", remainingParts);

                            Console.WriteLine($"Final title: {title}, final author: {author}");

                            break;
                        }
                    }
                }
            }

            title = title.Trim();

            if (string.IsNullOrWhiteSpace(title))
                title = "YouTube_Video";

            if (title.Length > 60)
                title = TruncateSmart(title);


            return title;
        }

        public static string CleanTitle(string title, string author)
        {
            List<string> toRemove = new List<string> { "Official Music Video", "Official Video", "Lyric Video", "Lyrics Video", "Official Audio", "Audio", "Official Audio Visualizer", "Official Song", "Full Album", "Deluxe Edition", "Lyrics" };

            title = new string(title.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());

            foreach (string subString in toRemove)
            {
                title = title.Replace("(" + subString + ")", "", true, CultureInfo.InvariantCulture);
                title = title.Replace("[" + subString + "]", "", true, CultureInfo.InvariantCulture);
            }

            Console.WriteLine("Title is " + title);

            author = CleanAuthor(author);

            title = Regex.Replace(title, @"\[.*?\]", "");
            title = title.Replace($"{author} - ", "", true, CultureInfo.InvariantCulture);
            title = title.Replace($"{author}-", "", true, CultureInfo.InvariantCulture);
            title = title.Replace($" - {author}", "", true, CultureInfo.InvariantCulture);
            title = title.Replace($"-{author}", "", true, CultureInfo.InvariantCulture);

            string[] titleParts = title.Split(new[] { " - " }, StringSplitOptions.None);

            Console.WriteLine("Title parts are " + string.Join(" | ", titleParts));

            if (titleParts.Length > 1)
            {
                for (int i = 0; i < titleParts.Length; i++)
                {
                    string normalizedPart = titleParts[i].Replace(" ", "");
                    string normalizedAuthor = author.Replace(" ", "");

                    if (normalizedPart.Contains(normalizedAuthor, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Match found at index {i}: {titleParts[i]}");

                        var remainingParts = titleParts.Where((part, index) => index != i);
                        title = string.Join(" - ", remainingParts);

                        Console.WriteLine($"Final title: {title}");

                        break;
                    }
                }
            }

            title = title.Trim();

            if (string.IsNullOrWhiteSpace(title))
                title = "YouTube_Video";

            if (title.Length > 60)
                title = TruncateSmart(title);


            return title;
        }

        public static string CleanUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            url = url.Trim();

            List<string> toRemove = new List<string> { "https://", "http://", "www.", "m.", "youtube.com/", "youtu.be/", "watch?v=" };

            foreach (string subString in toRemove)
            {
                url = url.Replace(subString, "", true, CultureInfo.InvariantCulture);
            }

            if (url.Length < 11)
                return "";

            url = url.Substring(0, 11);

            return url;
        }

        public static string TruncateSmart(string input, int maxLength = 60)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input;

            int lastSpace = input.LastIndexOf(' ', maxLength);

            string result;

            if (lastSpace > -1)
            {
                result = input.Substring(0, lastSpace);
            }
            else
            {
                result = input.Substring(0, maxLength);
            }

            return result + " ...";
        }

        // ---------- File saving methods ----------
#if ANDROID
        public void SaveAudio(Context context, string fileName, string inputFilePath)
        {
            if (!string.IsNullOrWhiteSpace(settings.FileUri))
                SaveAudioToChosenFolder(context, fileName, inputFilePath);
            else
                SaveAudioToDownloads(context, fileName, inputFilePath);
        }

        public void SaveAudioToChosenFolder(Context context, string fileName, string inputFilePath)
        {
            if (settings.FileUri == null)
                throw new InvalidOperationException("User has not chosen a folder yet.");

            Android.Net.Uri folderUri = Android.Net.Uri.Parse(settings.FileUri);

            var pickedDir = AndroidX.DocumentFile.Provider.DocumentFile.FromTreeUri(context, folderUri);
            var newFile = pickedDir.CreateFile("audio/mpeg", fileName);

            using var outputStream = context.ContentResolver.OpenOutputStream(newFile.Uri);
            using var inputStream = File.OpenRead(inputFilePath);
            inputStream.CopyTo(outputStream);
        }

        public static void SaveAudioToDownloads(Context context, string fileName, string inputFilePath)
        {
            ContentValues values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(MediaStore.IMediaColumns.MimeType, "audio/mpeg");
            values.Put(MediaStore.IMediaColumns.RelativePath, "Download/");

            Android.Net.Uri collection = MediaStore.Downloads.ExternalContentUri;
            ContentResolver resolver = context.ContentResolver;

            Android.Net.Uri fileUri = resolver.Insert(collection, values);

            if (fileUri != null)
            {
                using var outputStream = resolver.OpenOutputStream(fileUri);
                using var inputStream = File.OpenRead(inputFilePath);
                inputStream.CopyTo(outputStream);
            }
        }

        public void SaveVideo(Context context, string fileName, string inputFilePath)
        {
            if (!string.IsNullOrWhiteSpace(settings.FileUri))
                SaveVideoToChosenFolder(context, fileName, inputFilePath);
            else
                SaveVideoToDownloads(context, fileName, inputFilePath);
        }

        public void SaveVideoToChosenFolder(Context context, string fileName, string inputFilePath)
        {
            if (settings.FileUri == null)
                throw new InvalidOperationException("User has not chosen a folder yet.");

            Android.Net.Uri folderUri = Android.Net.Uri.Parse(settings.FileUri);

            var pickedDir = AndroidX.DocumentFile.Provider.DocumentFile.FromTreeUri(context, folderUri);
            var newFile = pickedDir.CreateFile("video/mp4", fileName);

            using var outputStream = context.ContentResolver.OpenOutputStream(newFile.Uri);
            using var inputStream = File.OpenRead(inputFilePath);
            inputStream.CopyTo(outputStream);
        }

        public static void SaveVideoToDownloads(Context context, string fileName, string inputFilePath)
        {
            ContentValues values = new ContentValues();
            values.Put(MediaStore.IMediaColumns.DisplayName, fileName);
            values.Put(MediaStore.IMediaColumns.MimeType, "video/mp4");
            values.Put(MediaStore.IMediaColumns.RelativePath, "Download/");

            Android.Net.Uri collection = MediaStore.Downloads.ExternalContentUri;
            ContentResolver resolver = context.ContentResolver;

            Android.Net.Uri fileUri = resolver.Insert(collection, values);

            if (fileUri != null)
            {
                using var outputStream = resolver.OpenOutputStream(fileUri);
                using var inputStream = File.OpenRead(inputFilePath);
                inputStream.CopyTo(outputStream);
            }
        }
#endif
#if WINDOWS
        public void SaveToDownloads(string NameOfFile, string sourceFilePath)
        {
            if (!File.Exists(sourceFilePath))
                throw new FileNotFoundException("Source file does not exist.", sourceFilePath);

            string filePath = "";

            if (!string.IsNullOrWhiteSpace(settings.FileUri))
                filePath = settings.FileUri;
            else
                filePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");

            string fileName = Path.GetFileName(sourceFilePath);
            string destinationPath = Path.Combine(filePath, NameOfFile);

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);

            File.Move(sourceFilePath, destinationPath);

            Console.WriteLine($"File moved to: {destinationPath}");
        }
#endif

        // ---------- Canceling ----------
        private void OnCancelClicked(object sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
#if ANDROID || WINDOWS
                FFmpegInterop.CancelFFmpeg();
#endif

                _downloadCts?.Cancel();

                DownloadStopped();

                ResetMainPageState(fastDwnld, false);
            });
        }

        private void DownloadStopped()
        {
            if (_downloadCts != null)
                _downloadCts.Dispose();
            settings.IsDownloadRunning = false;
        }

        // ---------- Navigation ----------
        private async void OpenSettings(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(Settings));
        }

        private async void OpenSearch(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(YouTubeSearch));
        }

        // ---------- Picker changed events ----------
        private void OnWantedFormatChanged(object sender, EventArgs e)
        {
            if (sender is not Picker picker)
                return;

            switch (picker.SelectedIndex)
            {
                case -1:
                    return;
                case 0:
                    mainPage.FormatPickerSelectedIndex = 0;
                    break;
                case 1:
                    mainPage.FormatPickerSelectedIndex = 1;
                    break;
                default:
                    return;
            }

            if (qualityPicker == null || audioOptions == null || videoOptions == null)
                return;

            switch (picker.SelectedIndex)
            {
                case 0:
                    mainPage.QualityPickerItemsSource = audioOptions.Values.ToList();
                    mainPage.QualityPickerSelectedIndex = 0;
                    break;
                case 1:
                    mainPage.QualityPickerItemsSource = videoOptions.Values.ToList();
                    mainPage.QualityPickerSelectedIndex = 0;
                    break;
            }
        }

        private void OnQualityChanged(object sender, EventArgs e)
        {
            if (qualityPicker == null || audioOptions == null || videoOptions == null)
                return;

            if (sender is not Picker picker)
                return;

            mainPage.QualityPickerSelectedIndex = picker.SelectedIndex;
        }

        // ---------- UI state management ----------
        private void InitializeMainPage()
        {
            mainPage.UrlEntryText = "";
            mainPage.DownloadOptionsIsVisible = true;
            mainPage.FormatPickerSelectedIndex = 0;
            mainPage.QualityPickerIsVisible = fastDwnld;
            mainPage.QualityPickerSelectedIndex = 0;
            mainPage.LoadButtonIsVisible = !fastDwnld;
            mainPage.LoadButtonIsEnabled = true;
            mainPage.DownloadButtonIsVisible = fastDwnld;
            mainPage.CancelButtonIsVisible = false;
            mainPage.DwnldProgressIsVisible = false;
            mainPage.DownloadIndicatorIsVisible = false;
            mainPage.StatusLabelFormattedText = "";
            mainPage.StatusLabelIsVisible = false;

            //Popup
            mainPage.PopupIsVisible = false;
        }

        private void ResetMainPageState(bool isQuickDownload, bool clearUrl = true)
        {
            mainPage.DownloadOptionsIsVisible = false;
            mainPage.FormatPickerIsEnabled = true;
            mainPage.QualityPickerIsEnabled = true;
            mainPage.QualityPickerSelectedIndex = 0;
            mainPage.DownloadButtonIsVisible = false;
            mainPage.CancelButtonIsVisible = false;
            mainPage.DwnldProgressIsVisible = false;
            mainPage.DownloadIndicatorIsVisible = false;
            mainPage.StatusLabelIsVisible = false;
            mainPage.UrlEntryText = clearUrl ? "" : mainPage.UrlEntryText;
            url = clearUrl ? "" : mainPage.UrlEntryText;

            mainPage.LoadButtonIsVisible = true;
            mainPage.LoadButtonIsEnabled = true;

            if (isQuickDownload)
            {
                QuickDownloadPage();
            }
        }

        private void QuickDownloadPage()
        {
            mainPage.DownloadOptionsIsVisible = true;
            mainPage.QualityPickerIsVisible = false;
            mainPage.LoadButtonIsVisible = false;
            mainPage.DownloadButtonIsVisible = true;
        }

        // ---------- Popup creation and deletion ----------

        public void ShowPopup(string title, string message, byte DefaultSuccessError = 0, string buttonText = "OK")
        {
            Color bgColor = DefaultSuccessError switch
            {
                0 => Color.FromArgb("#606060"), // Default - Grey
                1 => Color.FromArgb("#005500"), // Success - Green
                2 => Color.FromArgb("#aa0000"), // Error - Red
                _ => Color.FromArgb("#a5a5a5"), // Fallback to Default - Grey
            };
            mainPage.PopupBackground = bgColor;
            mainPage.PopupTitle = title;
            mainPage.PopupMessage = message;
            mainPage.PopupButtonText = buttonText;
            mainPage.PopupIsVisible = true;
        }

        private void OnClosePopupClicked(object sender, EventArgs e)
        {
            mainPage.PopupIsVisible = false;
        }

        // ---------- History list events ----------

        private void OnHistoryItemTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string term)
            {
                mainPage.UrlEntryText = term;
            }
        }

        private void OnRemoveHistoryItem(object sender, EventArgs e)
        {
            if (sender is ImageButton b && b.CommandParameter is string term)
            {
                Console.WriteLine($"Removing download item: {term}");
                settings.DownloadHistory.Remove(settings.DownloadHistory.FirstOrDefault(item => item.UrlOrID == term));
                settings.SaveExtraData();
                if (settings.DownloadHistory.Count() == 0)
                {
                    DownloadList.IsVisible = false;
                    EmptyHistory.IsVisible = true;
                }
            }
        }
    }
}