#if ANDROID
using Android.Content;
using Android.OS;
using YTCnv.FFmpeg;

namespace YTCnv
{
    public static class FFmpegInterop
    {
        private static SettingsSave settings = SettingsSave.Instance();

        private static bool registered = false;

        public static void RunFFmpegCommand(string command, byte audioVideoElse, string title)
        {
            var context = Android.App.Application.Context;

            Console.WriteLine("Registering FFmpegResultReceiver");

            if (!registered)
            {
                var filter = new IntentFilter(FFmpegBroadcasts.FFmpegFinishedAction);
                if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
                {
                    context.RegisterReceiver(settings.ffmpegReciever, filter, ReceiverFlags.NotExported);
                }
                else
                {
                    context.RegisterReceiver(settings.ffmpegReciever, filter);
                }

                registered = true;
            }
            
            Console.WriteLine("Starting FFmpegService with command: " + command);

            var intent = new Intent(context, typeof(FFmpegService));
            intent.PutExtra("command", command);
            intent.PutExtra("audioVideoElse", audioVideoElse);
            intent.PutExtra("title", title);
            context.StartService(intent);
        }

        public static void CancelFFmpeg()
        {
            var context = Android.App.Application.Context;
            var intent = new Intent(context, typeof(FFmpegService));
            intent.SetAction(FFmpegBroadcasts.FFmpegCancelAction);
            context.StartService(intent);
        }
    }
}

#endif
#if WINDOWS
using System.Diagnostics;

namespace YTCnv
{
    public static class FFmpegInterop
    {
        private static CancellationTokenSource? _cts;

        public static Task<int> RunFFmpegCommand(string arguments, Action<string>? onOutput = null)
        {
            _cts = new CancellationTokenSource();
            return RunFfmpegInternalAsync(arguments, onOutput, _cts.Token);
        }

        private static Task<int> RunFfmpegInternalAsync(string arguments, Action<string>? onOutput, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<int>();

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ffmpeg.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (s, e) => { if (e.Data != null) onOutput?.Invoke(e.Data); };
            process.ErrorDataReceived += (s, e) => { if (e.Data != null) onOutput?.Invoke(e.Data); };

            process.Exited += (s, e) =>
            {
                if (!tcs.Task.IsCompleted)
                    tcs.SetResult(process.ExitCode);
                process.Dispose();
            };

            token.Register(() =>
            {
                if (!process.HasExited)
                {
                    process.Kill(true);
                    tcs.TrySetCanceled(token);
                }
            });

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }

        public static void CancelFFmpeg()
        {
            _cts?.Cancel();
        }
    }
}
#endif