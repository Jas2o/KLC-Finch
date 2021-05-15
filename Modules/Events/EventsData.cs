using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace KLC_Finch.Modules {
    public class EventsData : INotifyPropertyChanged {

        public ObservableCollection<string> ListType { get; set; }
        public ObservableCollection<EventValue> ListEvent { get; set; }
        private ListCollectionView _listCollectionView;

        public EventsData() {
            ListType = new ObservableCollection<string>();
            ListEvent = new ObservableCollection<EventValue>();

            _listCollectionView = CollectionViewSource.GetDefaultView(ListEvent) as ListCollectionView;
            if (_listCollectionView != null) {
                _listCollectionView.IsLiveSorting = true;
                _listCollectionView.CustomSort = new CaseInsensitiveComparer(CultureInfo.InvariantCulture);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged {
            add {
                ((INotifyPropertyChanged)ListEvent).PropertyChanged += value;
            }

            remove {
                ((INotifyPropertyChanged)ListEvent).PropertyChanged -= value;
            }
        }

        public void EventsClear() {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListEvent.Clear();
            });
        }

        public void EventsAdd(EventValue ev) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListEvent.Add(ev);
            });
        }

        public void Filter(string txt) {
            _listCollectionView.Filter = new Predicate<object>(x => 
                ((EventValue)x).EventMessage.IndexOf(txt, StringComparison.OrdinalIgnoreCase) >= 0 ||
                ((EventValue)x).SourceName.IndexOf(txt, StringComparison.OrdinalIgnoreCase) >= 0
            );
        }
    }
}
