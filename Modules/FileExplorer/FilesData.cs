using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace KLC_Finch.Modules {
    public class FilesData : INotifyPropertyChanged {

        public ObservableCollection<KLCFile> ListFile { get; set; }
        private ListCollectionView _listCollectionView;

        public FilesData() {
            ListFile = new ObservableCollection<KLCFile>();

            _listCollectionView = CollectionViewSource.GetDefaultView(ListFile) as ListCollectionView;
            if (_listCollectionView != null) {
                _listCollectionView.IsLiveSorting = true;
                _listCollectionView.CustomSort = new CaseInsensitiveComparer(CultureInfo.InvariantCulture);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged {
            add {
                ((INotifyPropertyChanged)ListFile).PropertyChanged += value;
            }

            remove {
                ((INotifyPropertyChanged)ListFile).PropertyChanged -= value;
            }
        }

        public void FilesClear() {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListFile.Clear();
            });
        }

        public void FilesAdd(KLCFile kf) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListFile.Add(kf);
            });
        }

        public KLCFile GetFile(string valueName) {
            return ListFile.FirstOrDefault(x => x.Name == valueName); 
        }
    }
}
