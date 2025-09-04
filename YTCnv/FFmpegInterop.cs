#if ANDROID
using Com.Arthenica.Ffmpegkit;

namespace YTCnv
{
    public static class FFmpegInterop
    {
        public static async Task<bool> RunFFmpegCommand(string command)
        {
            var tcs = new TaskCompletionSource<FFmpegSession>();
            var callback = new FFmpegSessionCompleteCallback(tcs);

            try
            {
                FFmpegSession FFmpegSession = FFmpegKit.ExecuteAsync(command, callback);

                var session = await tcs.Task;
                var returnCode = session.ReturnCode;

                if (ReturnCode.IsSuccess(returnCode))
                {
                    Console.WriteLine("FFmpeg command executed successfully");
                    return true;
                }
                else
                {
                    Console.WriteLine($"FFmpeg failed with code: {returnCode} and message: {session.FailStackTrace}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while running FFmpeg: {ex.Message}");
                return false;
            }
        }

        public static Task DisposeOfSessions()
        {
            return Task.Run(() =>
            {
                try
                {
                    FFmpegKit.Cancel();

                    var sessions = FFmpegKitConfig.FFmpegSessions;
                    if (sessions != null && sessions.Count > 0)
                    {
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            try
                            {
                                sessions[i]?.Dispose();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Dispose session {i} failed: {ex.Message}");
                            }
                        }
                    }

                    FFmpegKitConfig.ClearSessions();
                    FFmpegKitConfig.Sessions.Clear();
                    FFmpegKitConfig.DisableRedirection();

                    Console.WriteLine("FFmpeg sessions were disposed of");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DisposeOfSessionsAsync error: {ex}");
                }
            });
        }

        public static Task CancelFFmpegCommand()
        {
            return Task.Run(() =>
            {
                try
                {
                    FFmpegKit.Cancel();

                    var sessions = FFmpegKitConfig.FFmpegSessions;
                    if (sessions != null && sessions.Count > 0)
                    {
                        for (int i = 0; i < sessions.Count; i++)
                        {
                            try
                            {
                                sessions[i]?.Dispose();
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Dispose session {i} failed: {ex.Message}");
                            }
                        }
                    }

                    FFmpegKitConfig.ClearSessions();
                    FFmpegKitConfig.Sessions.Clear();
                    FFmpegKitConfig.DisableRedirection();

                    Console.WriteLine("FFmpeg sessions were disposed of");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CancelFFmpegCommandAsync error: {ex}");
                }
            });
        }

        public static void SetFFmpegSessionMemory()
        {
            FFmpegKitConfig.SessionHistorySize = 0;
        }
    }

    public class FFmpegSessionCompleteCallback : Java.Lang.Object, IFFmpegSessionCompleteCallback
    {
        private readonly TaskCompletionSource<FFmpegSession> _tcs;

        public FFmpegSessionCompleteCallback(TaskCompletionSource<FFmpegSession> tcs)
        {
            _tcs = tcs;
        }

        public void Apply(FFmpegSession session)
        {
            Console.WriteLine("Apply() called");
            _tcs.TrySetResult(session);
        }
    }
}
#endif