using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KLC_Finch.Modules {
    public class EventsData : INotifyPropertyChanged {

        public ObservableCollection<string> ListType { get; set; }
        public ObservableCollection<EventValue> ListEvent { get; set; }

        public EventsData() {
            ListType = new ObservableCollection<string>();
            ListEvent = new ObservableCollection<EventValue>();
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
    }
}
