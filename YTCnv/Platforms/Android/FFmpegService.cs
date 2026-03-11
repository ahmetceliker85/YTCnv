using Android.App;
using Android.Content;
using Android.OS;
using Com.Arthenica.Ffmpegkit;

namespace YTCnv.FFmpeg
{
    [Service(Name = "com.pg_axis.ytcnv.FFmpegService", Exported = false, ForegroundServiceType = Android.Content.PM.ForegroundService.TypeDataSync)]
    public class FFmpegService : Service
    {
        public override IBinder OnBind(Intent intent) => null!;

        public override StartCommandResult OnStartCommand(Intent intent, StartCommandFlags flags, int startId)
        {
            var channelId = "ffmpeg_channel";
            var channel = new NotificationChannel(channelId, "FFmpeg Service", NotificationImportance.Low);
            var nm = (NotificationManager)GetSystemService(NotificationService);
            nm.CreateNotificationChannel(channel);

            var notification = new Notification.Builder(this, channelId)
                .SetContentTitle("FFmpeg running")
                .SetContentText("Processing media...")
                .SetSmallIcon(Resource.Drawable.icon)
                .SetColor(Android.Graphics.Color.ParseColor("#2b950f"))
                .SetCategory(Notification.CategoryService)
                .Build();

            StartForeground(1, notification);

            if (intent.Action == FFmpegBroadcasts.FFmpegCancelAction)
            {
                CancelCurrentCommand();
                return StartCommandResult.NotSticky;
            }

            FFmpegKitConfig.EnableLogCallback(new FFmpegLogCallback());

            var command = intent.GetStringExtra("command");
            byte audioVideoElse = (byte)intent.GetShortExtra("audioVideoElse", 3);
            string title = intent.GetStringExtra("title") ?? "Unknown";

            if (!string.IsNullOrEmpty(command))
            {
                var callback = new FFmpegSessionCompleteCallback(this, audioVideoElse, title);

                var ffmpegSession = FFmpegKit.ExecuteAsync(command, callback);
            }

            return StartCommandResult.NotSticky;
        }

        public void OnSessionFinished()
        {
            StopSelf();
        }

        public void CancelCurrentCommand()
        {
            FFmpegKit.Cancel();

            StopSelf();
        }
    }

    public class FFmpegLogCallback : Java.Lang.Object, ILogCallback
    {
        public void Apply(Log log)
        {
            Console.WriteLine($"[FFmpegKit] {log.Message}");
        }
    }

    public class FFmpegSessionCompleteCallback : Java.Lang.Object, IFFmpegSessionCompleteCallback
    {
        private readonly Context _context;
        private readonly byte _audioVideoElse;
        private readonly string _title;

        public FFmpegSessionCompleteCallback(Context context, byte audioVideoElse, string title)
        {
            _context = context;
            _audioVideoElse = audioVideoElse;
            _title = title;
        }

        public void Apply(FFmpegSession session)
        {
            Console.WriteLine($"[FFmpegKit] Return code: {session.ReturnCode.Value}");
            Console.WriteLine($"[FFmpegKit] Output: {session.Output}");
            Console.WriteLine($"[FFmpegKit] Fail stack trace: {session.FailStackTrace}");
            try
            {
                var intent = new Intent(FFmpegBroadcasts.FFmpegFinishedAction);
                intent.SetPackage(_context.PackageName);
                intent.PutExtra("returnCode", session.ReturnCode.Value);
                intent.PutExtra("audioVideoElse", (byte)_audioVideoElse);
                intent.PutExtra("title", _title);
                _context.SendBroadcast(intent);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception while sending broadcast: " + ex.Message);
            }

            if (_context is FFmpegService svc)
            {
                new Handler(Looper.MainLooper).Post(() => svc.OnSessionFinished());
            }
        }
    }
}
