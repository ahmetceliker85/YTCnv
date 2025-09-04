using System.Text.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace YTCnv
{
    public class SettingsSave : INotifyPropertyChanged
    {
        public const string ApiKeyPref = "YoutubeApiKey";

        private static SettingsSave instance;
        private static object instanceLock = new object();
        private static string settingsPath = Path.Combine(FileSystem.AppDataDirectory, "settings.json");

        public static SettingsSave Instance()
        {
            if (instance == null)
            {
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new SettingsSave();
                    }
                }
            }
            return instance;
        }

        //---------- Settings values ----------

        private bool use4K = false;
        public bool Use4K
        {
            get => use4K;
            set
            {
                if (use4K != value)
                {
                    use4K = value;
                    OnPropertyChanged(nameof(Use4K));
                    SaveSettings();
                }
            }
        }

        private bool quickDwnld = true;
        public bool QuickDwnld
        {
            get => quickDwnld;
            set
            {
                if (quickDwnld != value)
                {
                    quickDwnld = value;
                    OnPropertyChanged(nameof(QuickDwnld));
                    SaveSettings();
                }
            }
        }

        //---------- Singleton variables ----------

        public bool IsDownloadRunning = false;

        public bool IHaveId = false;
        public string ID = "";

        // ---------- MainPage ----------
        
        public string UrlEntryText;
        public bool DownloadOptionsIsVisible;
        public int FormatPickerSelectedIndex;
        public bool FormatPickerIsEnabled;
        public bool qualityPickerIsVisible;
        public int qualityPickerSelectedIndex;
        public bool qualityPickerIsEnabled;
        public bool LoadButtonIsVisible;
        public bool LoadButtonIsEnabled;
        public bool DownloadButtonIsVisible;
        public bool CancelButtonIsVisible;
        public bool DwnldProgressIsVisible;
        public bool DownloadIndicatorIsVisible;
        public string StatusLabelText;
        public bool StatusLabelIsVisible;

        //---------- Save/Load ----------

        public void SaveSettings()
        {
            SettingsClass settings = new SettingsClass
            {
                UseUpTo4K = Use4K,
                QuickDownload = QuickDwnld,
            };
            string json = JsonSerializer.Serialize(settings);
            File.WriteAllText(settingsPath, json);
        }

        public void LoadSettings()
        {
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                SettingsClass settings = JsonSerializer.Deserialize<SettingsClass>(json);
                Use4K = settings.UseUpTo4K;
                QuickDwnld = settings.QuickDownload;
            }
        }

        // ---------- Classes ----------

        public class SettingsClass
        {
            public bool UseUpTo4K { get; set; }
            public bool QuickDownload { get; set; }
        }

        public class MainPageClass
        {
            public string UrlEntryText { get; set; }
            public bool DownloadOptionsIsVisible { get; set; }
            public byte FormatPickerSelectedIndex { get; set; }
            public bool qualityPickerIsVisible { get; set; }
            public byte qualityPickerSelectedIndex { get; set; }
            public bool LoadButtonIsVisible { get; set; }
            public bool LoadButtonIsEnabled { get; set; }
            public bool DownloadButtonIsVisible { get; set; }
            public bool CancelButtonIsVisible { get; set; }
            public bool DwnldProgressIsVisible { get; set; }
            public bool DownloadIndicatorIsVisible { get; set; }
            public bool DownloadIndicatorIsRunning { get; set; }
            public string StatusLabelText { get; set; }
            public bool StatusLabelIsVisible { get; set; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

