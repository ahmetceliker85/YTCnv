using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
#if ANDROID
using YTCnv.FFmpeg;
#endif

namespace YTCnv
{
    public class SettingsSave : INotifyPropertyChanged
    {
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

        private bool dontShowUpdate = false;
        public bool DontShowUpdate
        {
            get => dontShowUpdate;
            set
            {
                if (dontShowUpdate != value)
                {
                    dontShowUpdate = value;
                    OnPropertyChanged(nameof(DontShowUpdate));
                    SaveSettings();
                }
            }
        }

        private ObservableCollection<string> searchHistory = new ObservableCollection<string>();
        public ObservableCollection<string> SearchHistory
        {
            get => searchHistory;
            set
            {
                if (searchHistory != value)
                {
                    searchHistory = value;
                    OnPropertyChanged(nameof(SearchHistory));
                    SaveSettings();
                }
            }
        }

        //---------- Singleton variables ----------

        public bool IsDownloadRunning = false;

        public bool IHaveId = false;
        public string ID = "";

#if ANDROID
        public FFmpegResultReceiver ffmpegReciever = new FFmpegResultReceiver();
#endif

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
                DontShowUpdatePopup = DontShowUpdate,
                SearchHistory = SearchHistory
            };
            string json = JsonConvert.SerializeObject(settings);
            File.WriteAllText(settingsPath, json);
        }

        public void LoadSettings()
        {
            if (File.Exists(settingsPath))
            {
                string json = File.ReadAllText(settingsPath);
                SettingsClass? settings = JsonConvert.DeserializeObject<SettingsClass>(json);
                if (settings != null)
                {
                    Use4K = settings.UseUpTo4K;
                    QuickDwnld = settings.QuickDownload;
                    DontShowUpdate = settings.DontShowUpdatePopup;
                    SearchHistory = settings.SearchHistory;
                }
            }
        }

        // ---------- Classes ----------

        public class SettingsClass
        {
            public bool UseUpTo4K { get; set; }
            public bool QuickDownload { get; set; }
            public bool DontShowUpdatePopup { get; set; }
            public ObservableCollection<string> SearchHistory { get; set; }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

