using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace KLC_Finch.Modules {
    public class ProcessesData : INotifyPropertyChanged {

        public ObservableCollection<ProcessValue> ListProcess { get; set; }
        private ListCollectionView _listCollectionView;

        public ProcessesData() {
            ListProcess = new ObservableCollection<ProcessValue>();

            _listCollectionView = CollectionViewSource.GetDefaultView(ListProcess) as ListCollectionView;
            if (_listCollectionView != null) {
                _listCollectionView.IsLiveSorting = true;
                _listCollectionView.CustomSort = new CaseInsensitiveComparer(CultureInfo.InvariantCulture);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged {
            add {
                ((INotifyPropertyChanged)ListProcess).PropertyChanged += value;
            }

            remove {
                ((INotifyPropertyChanged)ListProcess).PropertyChanged -= value;
            }
        }

        public void ProcessesClear() {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListProcess.Clear();
            });
        }

        public void ProcessesAdd(ProcessValue pv) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                //ProcessValue match = ListProcess.FirstOrDefault(x => x.PID == pv.PID && x.DisplayName == pv.DisplayName);
                //if (match != null)
                    //ListProcess.Remove(match);

                ListProcess.Add(pv);
            });
        }

        public void Filter(string txt) {
            _listCollectionView.Filter = new Predicate<object>(x => ((ProcessValue)x).DisplayName.IndexOf(txt, StringComparison.OrdinalIgnoreCase) >= 0);
        }

    }
}
