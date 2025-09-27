using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace YTCnv
{
    [Activity(
        Theme = "@style/Maui.SplashTheme",
        LaunchMode = LaunchMode.SingleTop,
        MainLauncher = true,
        ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation |
                               ConfigChanges.UiMode | ConfigChanges.ScreenLayout |
                               ConfigChanges.SmallestScreenSize | ConfigChanges.Density,
        ScreenOrientation = ScreenOrientation.Portrait)]
    [IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "text/plain")]
    [IntentFilter(new[] { Intent.ActionView },
                  Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
                  DataScheme = "https",
                  DataHost = "www.youtube.com")]
    [IntentFilter(new[] { Intent.ActionView },
                  Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
                  DataScheme = "https",
                  DataHost = "youtu.be")]
    public class MainActivity : MauiAppCompatActivity
    {
        private SettingsSave settings = SettingsSave.Instance();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            if (Build.VERSION.SdkInt >= BuildVersionCodes.Lollipop)
                Window.SetStatusBarColor(Android.Graphics.Color.Transparent);

            RequestNotificationPermission();
            HandleIntent(Intent);
        }

        protected override void OnNewIntent(Intent? intent)
        {
            base.OnNewIntent(intent);
            HandleIntent(intent);
        }

        public static void PickFolder(Activity activity, int requestCode = 42)
        {
            Intent intent = new Intent(Intent.ActionOpenDocumentTree);
            intent.AddFlags(ActivityFlags.GrantPersistableUriPermission |
                            ActivityFlags.GrantReadUriPermission |
                            ActivityFlags.GrantWriteUriPermission);

            activity.StartActivityForResult(intent, requestCode);
        }

        protected override void OnActivityResult(int requestCode, Result resultCode, Intent data)
        {
            base.OnActivityResult(requestCode, resultCode, data);

            if (requestCode == 42 && resultCode == Result.Ok)
            {
                Android.Net.Uri treeUri = data.Data;

                var flags = data.Flags & (ActivityFlags.GrantReadUriPermission | ActivityFlags.GrantWriteUriPermission);
                ContentResolver.TakePersistableUriPermission(treeUri, flags);

                settings.FileUri = treeUri.ToString();
                SaveFileNames(settings.FileUri);
            }
        }

        private void SaveFileNames(string UriString)
        {
            if (UriString != null)
            {
                var treeUri = Android.Net.Uri.Parse(UriString);
                var (root, folder) = GetFriendlyFolderName(treeUri);

                settings.MainFolder = root;
                settings.FinalFolder = $" - {folder}" ?? "";
            }
        }

        public static (string Root, string? EndFolder) GetFriendlyFolderName(Android.Net.Uri treeUri)
        {
            var docId = Android.Provider.DocumentsContract.GetTreeDocumentId(treeUri);
            var parts = docId.Split(':');

            string root;
            if (parts[0].Equals("primary", StringComparison.OrdinalIgnoreCase))
                root = "Internal storage";
            else
                root = "SD card";

            string? endFolder = "";
            if (parts.Length > 1 && !string.IsNullOrEmpty(parts[1]))
            {
                var subPath = parts[1];
                var segments = subPath.Split('/');
                endFolder = segments.Last();
            }
            return (root, endFolder);
        }

        private void HandleIntent(Intent? intent)
        {
            if (intent == null) return;

            if (Intent.ActionSend.Equals(intent.Action) && intent.Type == "text/plain")
            {
                string? sharedText = intent.GetStringExtra(Intent.ExtraText);
                if (!string.IsNullOrEmpty(sharedText))
                {
                    Console.WriteLine($"Shared text: {sharedText}");
                    settings.IHaveId = true;
                    settings.ID = sharedText.Trim();
                }
            }
            else if (Intent.ActionView.Equals(intent.Action))
            {
                Android.Net.Uri? uri = intent.Data;
                if (uri != null)
                {
                    string? youtubeUrl = uri.ToString();
                    if (!string.IsNullOrEmpty(youtubeUrl))
                    {
                        Console.WriteLine($"Opened via YouTube link: {youtubeUrl}");
                        settings.IHaveId = true;
                        settings.ID = youtubeUrl.Trim();
                    }
                }
            }
        }

        public void RequestNotificationPermission()
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                var permissionsToRequest = new List<string>();

                if (CheckSelfPermission(Manifest.Permission.PostNotifications) != Permission.Granted)
                    permissionsToRequest.Add(Manifest.Permission.PostNotifications);

                if (permissionsToRequest.Count > 0)
                {
                    ActivityCompat.RequestPermissions(this, permissionsToRequest.ToArray(), 101);
                }
            }
        }
    }
}
