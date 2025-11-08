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
        private static string EDataPath = Path.Combine(FileSystem.AppDataDirectory, "extra_data.json");

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

        private ObservableCollection<string> searchHistory = [];
        public ObservableCollection<string> SearchHistory
        {
            get => searchHistory;
            set
            {
                if (searchHistory != value)
                {
                    searchHistory = value;
                    OnPropertyChanged(nameof(SearchHistory));
                    SaveExtraData();
                }
            }
        }

        private ObservableCollection<HistoryItem> downloadHistory = [];
        public ObservableCollection<HistoryItem> DownloadHistory
        {
            get => downloadHistory;
            set
            {
                if (downloadHistory != value)
                {
                    downloadHistory = value;
                    OnPropertyChanged(nameof(DownloadHistory));
                    SaveExtraData();
                }
            }
        }

        private string fileUri = "";
        public string FileUri
        {
            get => fileUri;
            set
            {
                if (fileUri != value)
                {
                    fileUri = value;
                    OnPropertyChanged(nameof(FileUri));
                    SaveSettings();
                }
            }
        }


        private string mainFolder = DeviceInfo.Platform == DevicePlatform.Android ? "Internal storage" : (DeviceInfo.Platform == DevicePlatform.WinUI ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads") : "Unknown");
        public string MainFolder
        {
            get => mainFolder;
            set
            {
                if (mainFolder != value)
                {
                    mainFolder = value;
                    OnPropertyChanged(nameof(MainFolder));
                    SaveSettings();
                }
            }
        }

        private string finalFolder = DeviceInfo.Platform == DevicePlatform.Android ? " - Downloads" : "";
        public string FinalFolder
        {
            get => finalFolder;
            set
            {
                if (finalFolder != value)
                {
                    finalFolder = value;
                    OnPropertyChanged(nameof(FinalFolder));
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

        public HistoryItem GetHistoryItem(string title, string urlOrID)
        {
            return new HistoryItem
            {
                Title = title,
                UrlOrID = urlOrID
            };
        }

        //---------- Save/Load ----------

        public void SaveSettings()
        {
            SettingsClass settings = new SettingsClass
            {
                UseUpTo4K = Use4K,
                QuickDownload = QuickDwnld,
                DontShowUpdatePopup = DontShowUpdate,
                SavedFileUri = FileUri,
                MainFolderName = MainFolder,
                FinalFolderName = FinalFolder,
            };
            string json = JsonConvert.SerializeObject(settings);
            File.WriteAllText(settingsPath, json);
        }

        public void SaveExtraData()
        {
            ExtraData extraData = new ExtraData
            {
                SearchHistory = SearchHistory,
                DownloadHistory = DownloadHistory,
            };
            string json = JsonConvert.SerializeObject(extraData);
            File.WriteAllText(EDataPath, json);
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
                    FileUri = settings.SavedFileUri;
                    MainFolder = settings.MainFolderName;
                    FinalFolder = settings.FinalFolderName;
                }
            }
            if (File.Exists(EDataPath))
            {
                string json = File.ReadAllText(EDataPath);
                try
                {
                    ExtraData? extraData = JsonConvert.DeserializeObject<ExtraData>(json);
                    if (extraData != null)
                    {
                        SearchHistory = extraData.SearchHistory;
                        DownloadHistory = extraData.DownloadHistory;
                    }
                }
                catch
                {
                    SearchHistory = new ObservableCollection<string>();
                    DownloadHistory = new ObservableCollection<HistoryItem>();
                }
            }
        }

        // ---------- Classes ----------

        public class SettingsClass
        {
            public bool UseUpTo4K { get; set; }
            public bool QuickDownload { get; set; }
            public bool DontShowUpdatePopup { get; set; }
            public string SavedFileUri { get; set; }
            public string MainFolderName { get; set; }
            public string FinalFolderName { get; set; }
        }

        public class ExtraData
        {
            public ObservableCollection<string> SearchHistory { get; set; }
            public ObservableCollection<HistoryItem> DownloadHistory { get; set; }
        }

        public class HistoryItem
        {
            public string Title { get; set; }
            public string UrlOrID { get; set; }
        }

        // ---------- INotifyPropertyChanged Implementation ----------

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

}

