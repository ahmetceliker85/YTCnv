using Android.Content;
using Android.Content.PM;
using Android.OS;
using System.Threading.Tasks;

namespace YTCnv.FFmpeg
{
    [BroadcastReceiver(Exported = false)]
    public class FFmpegResultReceiver : BroadcastReceiver
    {
        public event Action<(int code, byte audioVideoElse, string fileTitle)>? OnFFmpegFinished;

        public FFmpegResultReceiver()
        {
        }

        public override void OnReceive(Context context, Intent intent)
        {
            if (intent.Action == FFmpegBroadcasts.FFmpegFinishedAction)
            {
                Console.WriteLine("FFmpegResultReceiver received FFmpegFinishedAction broadcast");

                var returnCode = intent.GetIntExtra("returnCode", -1);
                byte audioVideoElse = (byte)intent.GetShortExtra("audioVideoElse", 3);
                string title = intent.GetStringExtra("title") ?? "Unknown";
                OnFFmpegFinished?.Invoke((returnCode, audioVideoElse, title));
            }
        }
    }
}
