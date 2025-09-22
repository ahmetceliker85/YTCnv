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
            DownloadList.BindingContext = settings;
            DownloadHistoryFrame.HeightRequest = (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) - 500;
            DownloadList.HeightRequest = (DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density) - 550;
#if ANDROID
            FormatPicker.WidthRequest = 35;
#endif
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

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
                ResetMainPageState(fastDwnld);

                if (settings.IHaveId)
                {
                    settings.IHaveId = false;
                    UrlEntry.Text = settings.ID;
                    settings.UrlEntryText = UrlEntry.Text;
                }
            }
            else
            {
                RestoreMainPage();
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

            StatusLabel.IsVisible = false;
            settings.StatusLabelIsVisible = false;
            if (Connectivity.NetworkAccess == NetworkAccess.Internet)
            {
                await LoadVideoMetadata();
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
            settings.UrlEntryText = url;

            url = ExtractVideoID(url);

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
                Video? video = await YouTube.Videos.GetAsync(url).ConfigureAwait(false);

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

                if (video.Duration == null)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ResetMainPageState(fastDwnld);
                        await DisplayAlert("Live stream", "The video is a live stream, and therefore can’t be downloaded", "OK");
                    });
                    DownloadStopped();
                    return;
                }

                string title = CleanTitle(video.Title, video.Author.ChannelTitle);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        if (!settings.DownloadHistory.Any(item => item.UrlOrID == url))
                        {
                            SettingsSave.HistoryItem newItem = new SettingsSave.HistoryItem { Title = title, UrlOrID = url };
                            settings.DownloadHistory.Insert(0, newItem);
                            settings.SaveExtraData();
                            EmptyHistory.IsVisible = false;
                            DownloadList.IsVisible = true;
                            DownloadList.ScrollTo(newItem, ScrollToPosition.Start);
                        }
                        else
                        {
                            settings.DownloadHistory.Remove(settings.DownloadHistory.FirstOrDefault(item => item.UrlOrID == url));
                            SettingsSave.HistoryItem newItem = new SettingsSave.HistoryItem { Title = title, UrlOrID = url };
                            settings.DownloadHistory.Insert(0, newItem);
                            settings.SaveExtraData();
                            EmptyHistory.IsVisible = false;
                            DownloadList.IsVisible = true;
                            DownloadList.ScrollTo(newItem, ScrollToPosition.Start);
                        }
                    }
                });

                StreamManifest streamManifest = await YouTube.Videos.Streams.GetManifestAsync(url).ConfigureAwait(false);

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

                settings.IsDownloadRunning = false;
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
                }
                else if (ex.Message.Contains("403") || ex.Message.Contains("404"))
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ResetMainPageState(fastDwnld, false);
                        await DisplayAlert("Video unavailable", "The video is private, age-restricted, does not exist, or YouTube is just not feeling it today. Please try again", "OK");
                    });
                }
                else
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ResetMainPageState(fastDwnld);
                        await DisplayAlert("Error", ex.Message, "OK");
                    });
                }
                settings.IsDownloadRunning = false;
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
                ResetMainPageState(fastDwnld, false);
                await DisplayAlert("Lost connection", "Please connect to the internet", "OK");
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

            url = ExtractVideoID(url);

            if (string.IsNullOrWhiteSpace(url))
            {
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    ResetMainPageState(fastDwnld);
                    await DisplayAlert("No URL", "Please enter a YouTube URL", "OK");
                });
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
                MainThread.BeginInvokeOnMainThread(() =>
                {
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
                        ResetMainPageState(fastDwnld);
                        await DisplayAlert("Invalid URL", "Please enter a valid YouTube URL", "OK");
                    });
                    DownloadStopped();
                    return;
                }

                if (video.Duration == null || video.Duration == TimeSpan.Zero)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ResetMainPageState(fastDwnld);
                        await DisplayAlert("Live stream", "The video is a live stream, and therefore can’t be downloaded", "OK");
                    });
                    DownloadStopped();
                    return;
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
#if ANDROID
                    var context = Android.App.Application.Context;
                    var intent = new Intent(context, typeof(DownloadNotificationService));
                    context.StartForegroundService(intent);
#endif
                });

                string author = CleanAuthor(video.Author.ChannelTitle);

                string title = CleanTitle(video.Title, ref author);

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        if (!settings.DownloadHistory.Any(item => item.UrlOrID == url))
                        {
                            SettingsSave.HistoryItem newItem = new SettingsSave.HistoryItem { Title = title, UrlOrID = url };
                            settings.DownloadHistory.Insert(0, newItem);
                            settings.SaveExtraData();
                            EmptyHistory.IsVisible = false;
                            DownloadList.IsVisible = true;
                            DownloadList.ScrollTo(newItem, ScrollToPosition.Start);
                        }
                        else
                        {
                            settings.DownloadHistory.Remove(settings.DownloadHistory.FirstOrDefault(item => item.UrlOrID == url));
                            SettingsSave.HistoryItem newItem = new SettingsSave.HistoryItem { Title = title, UrlOrID = url };
                            settings.DownloadHistory.Insert(0, newItem);
                            settings.SaveExtraData();
                            EmptyHistory.IsVisible = false;
                            DownloadList.IsVisible = true;
                            DownloadList.ScrollTo(newItem, ScrollToPosition.Start);
                        }
                    }
                });

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
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ResetMainPageState(fastDwnld, false);
                        await DisplayAlert("Lost connection", "Please connect to the internet", "OK");
                    });

                    DeleteFiles();
                }
                else if (ex.Message.Contains("403") || ex.Message.Contains("404"))
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ResetMainPageState(fastDwnld, false);
                        await DisplayAlert("Video unavailable", "The video is private, age-restricted, does not exist, or YouTube is just not feeling it today. Please try again", "OK");
                    });
                    DeleteFiles();
                }
                else if (ex.Message.Contains("ID or URL"))
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        ResetMainPageState(fastDwnld);
                        await DisplayAlert("Invalid URL", "Please enter a valid YouTube URL", "OK");
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
#endif
#if ANDROID || WINDOWS
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

        // ---------- Title, author and URL cleaning methods ----------
        public static string CleanAuthor(string author)
        {
            author = Regex.Replace(author, @"(VEVO|OfficialVEVO|TV|- Topic)$", "", RegexOptions.IgnoreCase).Trim();

            return author;
        }

        public static string CleanTitle(string title, ref string author)
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

            if (titleParts.Length > 1)
            {
                for (int i = 0; i < titleParts.Length; i++)
                {
                    string normalizedPart = titleParts[i].Replace(" ", "");
                    string normalizedAuthor = author.Replace(" ", "");

                    if (normalizedPart.Equals(normalizedAuthor, StringComparison.OrdinalIgnoreCase))
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

            title = title.Trim();

            if (string.IsNullOrWhiteSpace(title))
                title = "YouTube_Video";

            if (title.Length > 60)
                title = TruncateSmart(title);


            return title;
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

                    if (normalizedPart.Equals(normalizedAuthor, StringComparison.OrdinalIgnoreCase))
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

        public static string ExtractVideoID(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;
            var regex = new Regex(@"^(?:https?:\/\/)?(?:www\.)?(?:youtube\.com\/(?:watch\?v=|embed\/|v\/|shorts\/)|youtu\.be\/)?([a-zA-Z0-9_-]{11})$");
            var match = regex.Match(url);
            if (match.Success && match.Groups.Count > 1)
            {
                return match.Groups[1].Value;
            }
            return null;
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

                settings.IsDownloadRunning = false;

                ResetMainPageState(fastDwnld, false);
            });
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
                    settings.FormatPickerSelectedIndex = 0;
                    break;
                case 1:
                    settings.FormatPickerSelectedIndex = 1;
                    break;
                default:
                    return;
            }

            if (qualityPicker == null || audioOptions == null || videoOptions == null)
                return;

            switch (picker.SelectedIndex)
            {
                case 0:
                    qualityPicker.ItemsSource = audioOptions.Values.ToList();
                    qualityPicker.SelectedIndex = 0;
                    settings.qualityPickerSelectedIndex = 0;
                    break;
                case 1:
                    qualityPicker.ItemsSource = videoOptions.Values.ToList();
                    qualityPicker.SelectedIndex = 0;
                    settings.qualityPickerSelectedIndex = 0;
                    break;
            }
        }

        private void OnQualityChanged(object sender, EventArgs e)
        {
            if (qualityPicker == null || audioOptions == null || videoOptions == null)
                return;

            if (sender is not Picker picker)
                return;

            settings.qualityPickerSelectedIndex = picker.SelectedIndex;
        }

        // ---------- UI state management ----------
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

        private void RestoreMainPage()
        {
            UrlEntry.Text = settings.UrlEntryText;
            downloadOptions.IsVisible = settings.DownloadOptionsIsVisible;
            FormatPicker.SelectedIndex = settings.FormatPickerSelectedIndex;
            qualityPicker.IsVisible = settings.qualityPickerIsVisible;
            qualityPicker.SelectedIndex = settings.qualityPickerSelectedIndex;
            LoadButton.IsVisible = settings.LoadButtonIsVisible;
            LoadButton.IsEnabled = settings.LoadButtonIsEnabled;
            DownloadButton.IsVisible = settings.DownloadButtonIsVisible;
            CancelButton.IsVisible = settings.CancelButtonIsVisible;
            DwnldProgress.IsVisible = settings.DwnldProgressIsVisible;
            DownloadIndicator.IsVisible = settings.DownloadIndicatorIsVisible;
            StatusLabel.Text = settings.StatusLabelText;
            StatusLabel.IsVisible = settings.StatusLabelIsVisible;
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

        private void OnHistoryItemTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is string term)
            {
                UrlEntry.Text = term;
                settings.UrlEntryText = term;
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