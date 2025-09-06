#if ANDROID
using Android.Content;
using Android.Provider;
#endif
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using Settings = YTCnv.Screens.Settings;
using YTCnv.Screens;
using System.Globalization;
using System.Text.RegularExpressions;

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

        private bool registered = false;

        public MainPage()
        {
            InitializeComponent();
            FormatPicker.SelectedIndex = 0;
            settings.LoadSettings();
            InitializeMainPage();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            fastDwnld = settings.QuickDwnld;

            if (!settings.IsDownloadRunning)
                ResetMainPageState(fastDwnld);

            if (settings.IHaveId)
            {
                settings.IHaveId = false;
                UrlEntry.Text = settings.ID;
                settings.UrlEntryText = UrlEntry.Text;
            }
        }

        private void OnLoadClicked(object sender, EventArgs e)
        {
            StatusLabel.IsVisible = false;
            settings.StatusLabelIsVisible = false;
            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                Task.Run(async () => await LoadVideoMetadata());
            }
            else
            {
                StatusLabel.IsVisible = true;
                settings.StatusLabelIsVisible = true;
                StatusLabel.Text = "Please connect to the internet";
                settings.StatusLabelText = "Please connect to the internet";
            }
        }

        private async Task LoadVideoMetadata()
        {
            settings.IsDownloadRunning = true;

            _4KChoice = settings.Use4K;

            LoadButton.IsEnabled = false;
            settings.LoadButtonIsEnabled = false;
            url = UrlEntry.Text;

            if (string.IsNullOrWhiteSpace(url))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("", "Please enter a YouTube URL", "OK");
                    ResetMainPageState(fastDwnld);
                });
                settings.IsDownloadRunning = false;
                return;
            }

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DownloadIndicator.IsVisible = true;
                settings.DownloadIndicatorIsVisible = true;
                StatusLabel.IsVisible = true;
                settings.StatusLabelIsVisible = true;
                StatusLabel.Text = "Retrieving video metadata";
                settings.StatusLabelText = "Retrieving video metadata";
            });

            try
            {
                Video? video = await YouTube.Videos.GetAsync(url);

                if (video == null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Invalid URL", "Please enter a valid YouTube URL", "OK");
                        ResetMainPageState(fastDwnld);
                    });
                    settings.IsDownloadRunning = false;
                    return;
                }

                StreamManifest streamManifest = await YouTube.Videos.Streams.GetManifestAsync(url);

                List<AudioOnlyStreamInfo> audioStreams = streamManifest.GetAudioOnlyStreams().Where(s => s.Container == Container.Mp4).OrderByDescending(s => s.Bitrate).ToList();
                audioOptions = audioStreams.GroupBy(s => (int)Math.Floor(s.Bitrate.KiloBitsPerSecond)).Select(g => g.OrderByDescending(s => s.Bitrate.KiloBitsPerSecond).First()).ToDictionary(s => s.Bitrate.KiloBitsPerSecond, s => $"{Math.Round(s.Bitrate.KiloBitsPerSecond)} kbps ({s.Size.MegaBytes:F1} MB)");

                List<VideoOnlyStreamInfo> videoStreams = _4KChoice ?
                    streamManifest.GetVideoOnlyStreams().OrderByDescending(s => s.VideoQuality.MaxHeight).ToList() :
                    streamManifest.GetVideoOnlyStreams().Where(s => s.Container == Container.Mp4 && s.VideoCodec.ToString().Contains("avc")).OrderByDescending(s => s.VideoQuality.MaxHeight).ToList();
                videoOptions = videoStreams.GroupBy(s => s.VideoQuality.MaxHeight).Select(g => g.OrderByDescending(s => s.VideoQuality.MaxHeight).First()).ToDictionary(s => s.VideoQuality.MaxHeight, s => $"{s.VideoQuality.Label} ({s.Size.MegaBytes:F1} MB)");

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    LoadButton.IsVisible = false;
                    settings.LoadButtonIsVisible = false;

                    LoadButton.IsEnabled = true;
                    settings.LoadButtonIsEnabled = true;

                    StatusLabel.IsVisible = false;
                    settings.StatusLabelIsVisible = false;

                    DownloadIndicator.IsVisible = false;
                    settings.DownloadIndicatorIsVisible = false;

                    downloadOptions.IsVisible = true;
                    settings.DownloadOptionsIsVisible = false;

                    qualityPicker.IsVisible = true;
                    settings.qualityPickerIsVisible = true;

                    FormatPicker.SelectedIndex = 0;
                    settings.FormatPickerSelectedIndex = 0;

                    qualityPicker.ItemsSource = audioOptions.Values.ToList();
                    qualityPicker.SelectedIndex = 0;
                    settings.qualityPickerSelectedIndex = 0;

                    DownloadButton.IsVisible = true;
                    settings.DownloadButtonIsVisible = true;
                });
            }
            catch (Exception ex)
            {
                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Lost connection", "Please connect to the internet", "OK");
                        ResetMainPageState(fastDwnld, false);
                    });
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Error", ex.Message, "OK");
                        ResetMainPageState(fastDwnld);
                    });
                }
                settings.IsDownloadRunning = false;
            }
        }

        private async void OnDownloadClicked(object sender, EventArgs e)
        {
            FormatPicker.IsEnabled = false;
            settings.FormatPickerIsEnabled = false;

            qualityPicker.IsEnabled = false;
            settings.qualityPickerIsEnabled = false;

            if (_downloadCts != null)
                _downloadCts.Dispose();
            _downloadCts = null;
            _downloadCts = new CancellationTokenSource();

            StatusLabel.IsVisible = false;
            settings.StatusLabelIsVisible = false;

            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                await DoTheThing(fastDwnld);
            }
            else
            {
                await DisplayAlert("Lost connection", "Please connect to the internet", "OK");
                ResetMainPageState(fastDwnld, false);
            }
        }

        private async Task DoTheThing(bool useNewUrl)
        {
            settings.IsDownloadRunning = true;

            _4KChoice = settings.Use4K;

            MainThread.BeginInvokeOnMainThread(() =>
            {
                DownloadButton.IsVisible = false;
                settings.DownloadButtonIsVisible = false;

                CancelButton.IsVisible = true;
                settings.CancelButtonIsVisible = true;
            });

            Console.WriteLine("Download started");

            int selectedFormat = settings.FormatPickerSelectedIndex;

            Progress<double> progress = new Progress<double>(p =>
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DwnldProgress.Progress = p;
                });
            });

            if (useNewUrl)
                url = UrlEntry.Text;

            if (string.IsNullOrWhiteSpace(url))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await DisplayAlert("No URL", "Please enter a YouTube URL", "OK");
                    ResetMainPageState(fastDwnld);
                });
                DownloadStopped();
                return;
            }

            string m4aPath = Path.Combine(FileSystem.CacheDirectory, $"audio.m4a");
            string mp4Path = Path.Combine(FileSystem.CacheDirectory, "video.mp4");
            string semiOutput = Path.Combine(FileSystem.AppDataDirectory, "semi-outputVideo.mp4");
            string semiOutputAudio = Path.Combine(FileSystem.AppDataDirectory, "semi-outputAudio.mp3");
            string imagePath = Path.Combine(FileSystem.CacheDirectory, "thumbnail.jpg");

            string title = "";

            if (File.Exists(imagePath))
                File.Delete(imagePath);

            try
            {
                MainThread.BeginInvokeOnMainThread(() =>
                {
#if ANDROID
                    var context = Android.App.Application.Context;
                    var intent = new Intent(context, typeof(DownloadNotificationService));
                    context.StartForegroundService(intent);
#endif

                    DownloadIndicator.IsVisible = true;
                    settings.DownloadIndicatorIsVisible = true;

                    StatusLabel.IsVisible = true;
                    settings.StatusLabelIsVisible = true;
                    StatusLabel.Text = "Retrieving video metadata";
                    settings.StatusLabelText = "Retrieving video metadata";
                });

                Video? video = await YouTube.Videos.GetAsync(url).ConfigureAwait(false);

                if (video == null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        await DisplayAlert("Invalid URL", "Please enter a valid YouTube URL", "OK");
                        ResetMainPageState(fastDwnld);
                    });
                    DownloadStopped();
                    return;
                }

                string author = CleanAuthor(video.Author.ChannelTitle);

                title = CleanTitle(video.Title, author);

                string thumbnailUrl = video.Thumbnails.GetWithHighestResolution().Url;
                byte[] bytes = await http.GetByteArrayAsync(thumbnailUrl);
                await File.WriteAllBytesAsync(imagePath, bytes);

                if (!registered)
                {
#if ANDROID
                    settings.ffmpegReciever.OnFFmpegFinished += result =>
                    {
                        if (result.code == 0)
                        {
                            FinishUp(result.audioVideoElse);
                        }
                        else
                        {
                            itScrewedUp();
                        }
                    };

                    registered = true;
#endif
                }

                MainThread.BeginInvokeOnMainThread(() => StatusLabel.Text = "Retrieving video");
                settings.StatusLabelText = "Retrieving video";

                StreamManifest streamManifest = await YouTube.Videos.Streams.GetManifestAsync(url, _downloadCts.Token).ConfigureAwait(false);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    DownloadIndicator.IsVisible = false;
                    settings.DownloadIndicatorIsVisible = false;

                    DwnldProgress.IsVisible = true;
                    settings.DwnldProgressIsVisible = true;

                    StatusLabel.Text = $"Downloading {title}";
                    settings.StatusLabelText = $"Downloading {title}";
                });

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

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DwnldProgress.IsVisible = false;
                        settings.DwnldProgressIsVisible = false;

                        DownloadIndicator.IsVisible = true;
                        settings.DownloadIndicatorIsVisible = true;

                        StatusLabel.Text = "Adding metadata";
                        settings.StatusLabelText = "Adding metadata";
                    });

                    GC.AddMemoryPressure(audioStream.Size.Bytes + 3);
#if ANDROID
                    MainThread.BeginInvokeOnMainThread(() => FFmpegInterop.RunFFmpegCommand($"-y -i \"{m4aPath}\" -i \"{imagePath}\" -map 0:a -map 1:v -c:a libmp3lame -b:a 128k -c:v mjpeg -disposition:v attached_pic -metadata:s:v title=\"Album cover\" -metadata:s:v comment=\"Cover\" -metadata title=\"{title}\" -metadata artist=\"{author}\"  -threads 1 \"{semiOutputAudio}\"", 1));
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

                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        DwnldProgress.IsVisible = false;
                        settings.DwnldProgressIsVisible = false;

                        DownloadIndicator.IsVisible = true;
                        settings.DownloadIndicatorIsVisible = true;

                        StatusLabel.Text = "Joining audio and video";
                        settings.StatusLabelText = "Joining audio and video";
                    });

                    GC.AddMemoryPressure(videoStream.Size.Bytes + 3);
#if ANDROID
                    if (_4KChoice)
                    {
                        if (isMoreThan1080p)
                            MainThread.BeginInvokeOnMainThread(() => FFmpegInterop.RunFFmpegCommand($"-y -i \"{mp4Path}\" -i \"{m4aPath}\" -c:v libx264 -pix_fmt yuv420p -preset faster -crf 23 -c:a copy -map 0:v:0 -map 1:a:0 -shortest -metadata title=\"{title}\" -metadata artist=\"{author}\" \"{semiOutput}\"", 2));
                        else
                            MainThread.BeginInvokeOnMainThread(() => FFmpegInterop.RunFFmpegCommand($"-y -i \"{mp4Path}\" -i \"{m4aPath}\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 -shortest -metadata title=\"{title}\" -metadata artist=\"{author}\" \"{semiOutput}\"", 2));
                    }
                    else
                        MainThread.BeginInvokeOnMainThread(() => FFmpegInterop.RunFFmpegCommand($"-y -i \"{mp4Path}\" -i \"{m4aPath}\" -c:v copy -c:a copy -map 0:v:0 -map 1:a:0 -shortest -metadata title=\"{title}\" -metadata artist=\"{author}\" \"{semiOutput}\"", 2));
#endif
                }
            }
            catch (OperationCanceledException)
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    ResetMainPageState(fastDwnld, false);
                    await DisplayAlert("Canceled", "The download was cancelled.", "OK");

                    DeleteFiles();
                });

#if ANDROID
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var context = Android.App.Application.Context;
                    var stopIntent = new Intent(context, Java.Lang.Class.FromType(typeof(DownloadNotificationService)));
                    context.StopService(stopIntent);
                });

                FFmpegInterop.CancelFFmpeg();
#endif

                DownloadStopped();
            }
            catch (Exception ex)
            {
                if (Connectivity.NetworkAccess != NetworkAccess.Internet)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ResetMainPageState(fastDwnld, false);
                        await DisplayAlert("Lost connection", "Please connect to the internet", "OK");
                    });

                    DeleteFiles();
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        Console.WriteLine(ex.Message);
                        ResetMainPageState(fastDwnld);
                        await DisplayAlert("Error", ex.Message, "OK");
                    });

                    DeleteFiles();
                }
#if ANDROID
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    var context = Android.App.Application.Context;
                    var stopIntent = new Intent(context, Java.Lang.Class.FromType(typeof(DownloadNotificationService)));
                    context.StopService(stopIntent);
                });

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

            void FinishUp(byte audioVideoElse)
            {
                Console.WriteLine("FFmpeg process finally finished");
#if ANDROID
                switch (audioVideoElse)
                {
                    case 1:
                        SaveAudioToDownloads(Android.App.Application.Context, title + ".mp3", semiOutputAudio);
                        break;
                    case 2:
                        SaveVideoToDownloads(Android.App.Application.Context, title + ".mp4", semiOutput);
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

                FFmpegInterop.CancelFFmpeg();
#endif
                DeleteFiles();
                DownloadStopped();

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    ResetMainPageState(fastDwnld);
                    await DisplayAlert("Finished", "The download has completed successfully.", "OK");
                });
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

                FFmpegInterop.CancelFFmpeg();
#endif

                DeleteFiles();
                DownloadStopped();

                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    ResetMainPageState(fastDwnld);
                    await DisplayAlert("Failed", "The app failed to add metadata and save the file.", "OK");
                });
            }
        }

        public static string CleanAuthor(string author)
        {
            author = author.Replace(" - Topic", "", true, CultureInfo.InvariantCulture);
            author = author.Replace("OfficialVEVO", "", true, CultureInfo.InvariantCulture);

            return author;
        }

        public static string CleanTitle(string title, string author)
        {
            List<string> toRemove = new List<string> { "Official Music Video", "Official Video", "Official Audio", "Audio", "Official Audio Visualizer", "Official Song", "Full Album", "Deluxe Edition", "Lyrics" };

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

            for (int i = 0; i < titleParts.Length; i++)
            {
                if (titleParts[i].Contains(author, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Match found at index {i}: {titleParts[i]}");

                    var remainingParts = titleParts.Where((part, index) => index != i);

                    title = string.Join(" ", remainingParts);

                    break;
                }
            }

            title = title.Trim();

            if (string.IsNullOrWhiteSpace(title))
                title = "YouTube_Audio";

            if (title.Length > 60)
                title = title.Substring(0, 60);


            return title;
        }

#if ANDROID
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

        private async void OnCancelClicked(object sender, EventArgs e)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
#if ANDROID
                FFmpegInterop.CancelFFmpeg();
#endif

                _downloadCts?.Cancel();

                settings.IsDownloadRunning = false;

                ResetMainPageState(fastDwnld, false);
            });
        }

        private async void OpenSettings(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(Settings));
        }

        private async void OpenSearch(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync(nameof(YouTubeSearch));
        }

        private void InitializeMainPage()
        {
            settings.UrlEntryText = UrlEntry.Text;
            settings.DownloadOptionsIsVisible = downloadOptions.IsVisible;
            settings.FormatPickerSelectedIndex = FormatPicker.SelectedIndex;
            settings.qualityPickerIsVisible = qualityPicker.IsVisible;
            settings.qualityPickerSelectedIndex = qualityPicker.SelectedIndex;
            settings.LoadButtonIsVisible = LoadButton.IsVisible;
            settings.LoadButtonIsEnabled = LoadButton.IsEnabled;
            settings.DownloadButtonIsVisible = DownloadButton.IsVisible;
            settings.CancelButtonIsVisible = CancelButton.IsVisible;
            settings.DwnldProgressIsVisible = DwnldProgress.IsVisible;
            settings.DownloadIndicatorIsVisible = DownloadIndicator.IsVisible;
            settings.StatusLabelText = StatusLabel.Text;
            settings.StatusLabelIsVisible = StatusLabel.IsVisible;
        }

        private void OnWantedFormatChanged(object sender, EventArgs e)
        {
            if (qualityPicker == null || audioOptions == null || videoOptions == null)
                return;

            if (sender is not Picker picker)
                return;

            switch (picker.SelectedIndex)
            {
                case 0:
                    settings.FormatPickerSelectedIndex = 0;
                    qualityPicker.ItemsSource = audioOptions.Values.ToList();
                    qualityPicker.SelectedIndex = 0;
                    settings.qualityPickerSelectedIndex = 0;
                    break;
                case 1:
                    settings.FormatPickerSelectedIndex = 1;
                    qualityPicker.ItemsSource = videoOptions.Values.ToList();
                    qualityPicker.SelectedIndex = 0;
                    settings.qualityPickerSelectedIndex = 0;
                    break;
            }
        }

        private void ResetMainPageState(bool isQuickDownload, bool clearUrl = true)
        {
            downloadOptions.IsVisible = false;
            settings.DownloadOptionsIsVisible = false;

            FormatPicker.IsEnabled = true;
            settings.FormatPickerIsEnabled = true;

            qualityPicker.IsEnabled = true;
            settings.qualityPickerIsEnabled = true;

            qualityPicker.SelectedIndex = 0;
            settings.qualityPickerSelectedIndex = 0;

            DownloadButton.IsVisible = false;
            settings.DownloadButtonIsVisible = false;

            CancelButton.IsVisible = false;
            settings.CancelButtonIsVisible = false;

            DwnldProgress.IsVisible = false;
            settings.DwnldProgressIsVisible = false;

            DownloadIndicator.IsVisible = false;
            settings.DownloadIndicatorIsVisible = false;

            StatusLabel.IsVisible = false;
            settings.StatusLabelIsVisible = false;

            UrlEntry.Text = clearUrl ? "" : settings.UrlEntryText;
            settings.UrlEntryText = clearUrl ? "" : settings.UrlEntryText;
            url = "";

            LoadButton.IsVisible = true;
            settings.LoadButtonIsVisible = true;

            LoadButton.IsEnabled = true;
            settings.LoadButtonIsEnabled = true;

            if (isQuickDownload)
            {
                QuickDownloadPage();
            }
        }

        private void QuickDownloadPage()
        {
            downloadOptions.IsVisible = true;
            settings.DownloadOptionsIsVisible = true;

            qualityPicker.IsVisible = false;
            settings.qualityPickerIsVisible = false;

            LoadButton.IsVisible = false;
            settings.LoadButtonIsVisible = false;

            DownloadButton.IsVisible = true;
            settings.DownloadButtonIsVisible = true;
        }

        private void DownloadStopped()
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_downloadCts != null)
                    _downloadCts.Dispose();
                _downloadCts = null;
                settings.IsDownloadRunning = false;
            });
        }

        private void OnQualityChanged(object sender, EventArgs e)
        {
            if (qualityPicker == null || audioOptions == null || videoOptions == null)
                return;

            if (sender is not Picker picker)
                return;

            settings.qualityPickerSelectedIndex = picker.SelectedIndex;
        }
    }
}