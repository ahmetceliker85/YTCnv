using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace YTCnv.Screens
{
    public class MainPageBinding: INotifyPropertyChanged
    {
        private static MainPageBinding instance;
        private static object instanceLock = new object();

        public static MainPageBinding Instance()
        {
            if (instance == null)
            {
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new MainPageBinding();
                    }
                }
            }
            return instance;
        }

        // ---------- Binding values ----------

        private string urlEntryText;
        public string UrlEntryText
        {
            get => urlEntryText;
            set
            {
                if (urlEntryText != value)
                {
                    urlEntryText = value;
                    OnPropertyChanged(nameof(UrlEntryText));
                }
            }
        }

        private bool downloadOptionsIsVisible;
        public bool DownloadOptionsIsVisible
        {
            get => downloadOptionsIsVisible;
            set
            {
                if (downloadOptionsIsVisible != value)
                {
                    downloadOptionsIsVisible = value;
                    OnPropertyChanged(nameof(DownloadOptionsIsVisible));
                }
            }
        }

        private int formatPickerSelectedIndex;
        public int FormatPickerSelectedIndex
        {
            get => formatPickerSelectedIndex;
            set
            {
                if (formatPickerSelectedIndex != value)
                {
                    formatPickerSelectedIndex = value;
                    OnPropertyChanged(nameof(FormatPickerSelectedIndex));
                }
            }
        }

        private bool formatPickerIsEnabled;
        public bool FormatPickerIsEnabled
        {
            get => formatPickerIsEnabled;
            set
            {
                if (formatPickerIsEnabled != value)
                {
                    formatPickerIsEnabled = value;
                    OnPropertyChanged(nameof(FormatPickerIsEnabled));
                }
            }
        }

        private bool qualityPickerIsVisible;
        public bool QualityPickerIsVisible
        {
            get => qualityPickerIsVisible;
            set
            {
                if (qualityPickerIsVisible != value)
                {
                    qualityPickerIsVisible = value;
                    OnPropertyChanged(nameof(QualityPickerIsVisible));
                }
            }
        }

        private List<string> qualityPickerItemsSource = [];
        public List<string> QualityPickerItemsSource
        {
            get => qualityPickerItemsSource;
            set
            {
                if (qualityPickerItemsSource != value)
                {
                    qualityPickerItemsSource = value;
                    OnPropertyChanged(nameof(QualityPickerItemsSource));
                }
            }
        }

        private int qualityPickerSelectedIndex;
        public int QualityPickerSelectedIndex
        {
            get => qualityPickerSelectedIndex;
            set
            {
                if (qualityPickerSelectedIndex != value)
                {
                    qualityPickerSelectedIndex = value;
                    OnPropertyChanged(nameof(QualityPickerSelectedIndex));
                }
            }
        }

        private bool qualityPickerIsEnabled;
        public bool QualityPickerIsEnabled
        {
            get => qualityPickerIsEnabled;
            set
            {
                if (qualityPickerIsEnabled != value)
                {
                    qualityPickerIsEnabled = value;
                    OnPropertyChanged(nameof(QualityPickerIsEnabled));
                }
            }
        }

        private bool loadButtonIsVisible;
        public bool LoadButtonIsVisible
        {
            get => loadButtonIsVisible;
            set
            {
                if (loadButtonIsVisible != value)
                {
                    loadButtonIsVisible = value;
                    OnPropertyChanged(nameof(LoadButtonIsVisible));
                }
            }
        }

        private bool loadButtonIsEnabled;
        public bool LoadButtonIsEnabled
        {
            get => loadButtonIsEnabled;
            set
            {
                if (loadButtonIsEnabled != value)
                {
                    loadButtonIsEnabled = value;
                    OnPropertyChanged(nameof(LoadButtonIsEnabled));
                }
            }
        }

        private bool downloadButtonIsVisible;
        public bool DownloadButtonIsVisible
        {
            get => downloadButtonIsVisible;
            set
            {
                if (downloadButtonIsVisible != value)
                {
                    downloadButtonIsVisible = value;
                    OnPropertyChanged(nameof(DownloadButtonIsVisible));
                }
            }
        }

        private bool cancelButtonIsVisible;
        public bool CancelButtonIsVisible
        {
            get => cancelButtonIsVisible;
            set
            {
                if (cancelButtonIsVisible != value)
                {
                    cancelButtonIsVisible = value;
                    OnPropertyChanged(nameof(CancelButtonIsVisible));
                }
            }
        }

        private bool dwnldProgressIsVisible;
        public bool DwnldProgressIsVisible
        {
            get => dwnldProgressIsVisible;
            set
            {
                if (dwnldProgressIsVisible != value)
                {
                    dwnldProgressIsVisible = value;
                    OnPropertyChanged(nameof(DwnldProgressIsVisible));
                }
            }
        }

        private bool downloadIndicatorIsVisible;
        public bool DownloadIndicatorIsVisible
        {
            get => downloadIndicatorIsVisible; 
            set
            {
                if (downloadIndicatorIsVisible != value)
                {
                    downloadIndicatorIsVisible = value;
                    OnPropertyChanged(nameof(DownloadIndicatorIsVisible));
                }
            }
        }

        private FormattedString statusLabelFormattedText;
        public FormattedString StatusLabelFormattedText
        {
            get => statusLabelFormattedText;
            set
            {
                if (statusLabelFormattedText != value)
                {
                    statusLabelFormattedText = value;
                    OnPropertyChanged(nameof(StatusLabelFormattedText));
                }
            }
        }

        private bool statusLabelIsVisible;
        public bool StatusLabelIsVisible
        {
            get => statusLabelIsVisible;
            set
            {
                if (statusLabelIsVisible != value)
                {
                    statusLabelIsVisible = value;
                    OnPropertyChanged(nameof(StatusLabelIsVisible));
                }
            }
        }

        // ---------- Popup data ----------

        private Color popupBackground;
        public Color PopupBackground
        {
            get => popupBackground;
            set
            {
                if (popupBackground != value)
                {
                    popupBackground = value;
                    OnPropertyChanged(nameof(PopupBackground));
                }
            }
        }

        private string popupTitle;
        public string PopupTitle
        {
            get => popupTitle;
            set
            {
                if (popupTitle != value)
                {
                    popupTitle = value;
                    OnPropertyChanged(nameof(PopupTitle));
                }
            }
        }

        private string popupMessage;
        public string PopupMessage
        {
            get => popupMessage;
            set
            {
                if (popupMessage != value)
                {
                    popupMessage = value;
                    OnPropertyChanged(nameof(PopupMessage));
                }
            }
        }

        private string popupButtonText;
        public string PopupButtonText
        {
            get => popupButtonText;
            set
            {
                if (popupButtonText != value)
                {
                    popupButtonText = value;
                    OnPropertyChanged(nameof(PopupButtonText));
                }
            }
        }

        private bool popupIsVisible;
        public bool PopupIsVisible
        {
            get => popupIsVisible;
            set
            {
                if (popupIsVisible != value)
                {
                    popupIsVisible = value;
                    OnPropertyChanged(nameof(PopupIsVisible));
                }
            }
        }

        // ---------- INotifyPropertyChanged implementation ----------

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
