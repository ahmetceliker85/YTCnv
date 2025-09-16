#if ANDROID
using Android.App;
using Android.Content;
#elif WINDOWS
using Windows.Storage;
using Windows.Storage.AccessCache;
using Windows.Storage.Pickers;
using WinRT.Interop;
#endif
using System.Threading.Tasks;

namespace YTCnv.Screens;

public partial class Settings : ContentPage
{
    private SettingsSave settings = SettingsSave.Instance();

    public Settings()
    {
        InitializeComponent();
        BindingContext = settings;
        VersionLabel.Text = $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
    }

    private async void GoBack(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("///MainPage");
    }

    private async void ChangeDestination(object sender, EventArgs e)
    {
#if ANDROID
        MainActivity.PickFolder(Platform.CurrentActivity as MainActivity);
#elif WINDOWS
        await PickAndSaveFolderAsync();
#endif
    }

#if WINDOWS
    public async Task PickAndSaveFolderAsync()
    {
        var picker = new FolderPicker();
        picker.FileTypeFilter.Add("*");

        var hwnd = WindowNative.GetWindowHandle(App.Current.Windows[0].Handler.PlatformView);
        InitializeWithWindow.Initialize(picker, hwnd);

        StorageFolder folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            StorageApplicationPermissions.FutureAccessList.AddOrReplace("PickedFolderToken", folder);

            settings.FileUri = folder.Path;
            settings.MainFolder = folder.Path;
            settings.FinalFolder = "";
        }
    }
#endif
}