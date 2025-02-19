using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KLC_Finch.Modules {
    public class AssetData : INotifyPropertyChanged {

        public ObservableCollection<AssetValue> ListInfo { get; set; }

        public AssetData() {
            ListInfo = new ObservableCollection<AssetValue>();
        }

        public event PropertyChangedEventHandler PropertyChanged {
            add {
                ((INotifyPropertyChanged)ListInfo).PropertyChanged += value;
            }

            remove {
                ((INotifyPropertyChanged)ListInfo).PropertyChanged -= value;
            }
        }

    }
}
