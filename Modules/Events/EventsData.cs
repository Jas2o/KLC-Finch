using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KLC_Finch.Modules {
    public class EventsData {

        public ObservableCollection<string> ListType { get; set; }
        public ObservableCollection<EventValue> ListEvent { get; set; }

        public EventsData() {
            ListType = new ObservableCollection<string>();
            ListEvent = new ObservableCollection<EventValue>();
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
