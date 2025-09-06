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

        public static void RunFFmpegCommand(string command, byte audioVideoElse)
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
