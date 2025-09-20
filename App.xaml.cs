using YTCnv.Screens;

namespace YTCnv
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(Settings), typeof(Settings));
            Routing.RegisterRoute(nameof(YouTubeSearch), typeof(YouTubeSearch));

            Application.Current.UserAppTheme = AppTheme.Dark;

            MainPage = new AppShell();
        }
    }
}
