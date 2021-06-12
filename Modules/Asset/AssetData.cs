using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

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
