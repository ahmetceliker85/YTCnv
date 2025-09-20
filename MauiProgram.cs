using Microsoft.Extensions.Logging;
using CommunityToolkit.Maui;
using Microsoft.Maui.LifecycleEvents;
using Microsoft.Maui.Platform;
using System.Runtime.InteropServices;
#if WINDOWS
using Microsoft.UI.Windowing;
#endif

namespace YTCnv
{
    public static class MauiProgram
    {
#if WINDOWS
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        const int SW_MAXIMIZE = 3;
#endif

        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .ConfigureLifecycleEvents(events =>
                {
#if WINDOWS
                    events.AddWindows(windows =>
                    {
                        windows.OnWindowCreated(winuiWindow =>
                        {
                            IntPtr hWnd = WinRT.Interop.WindowNative.GetWindowHandle(winuiWindow);
                            
                            ShowWindow(hWnd, SW_MAXIMIZE);
                        });
                    });
#endif
                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("Poppins-Regular.ttf", "Poppins");
                });

#if DEBUG
    		builder.Logging.AddDebug();
#endif

            return builder.Build();
        }
    }
}
