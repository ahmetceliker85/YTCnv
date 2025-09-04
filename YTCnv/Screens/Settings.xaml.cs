using System.Threading.Tasks;

namespace YTCnv.Screens;

public partial class Settings : ContentPage
{
    private SettingsSave settings = SettingsSave.Instance();

    public Settings()
    {
        InitializeComponent();
        BindingContext = this;
        use4kSwitch.BindingContext = settings;
        enableQuickDwnld.BindingContext = settings;
        VersionLabel.Text = $"{AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
    }

    private async void GoBack(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("///MainPage");
    }

}