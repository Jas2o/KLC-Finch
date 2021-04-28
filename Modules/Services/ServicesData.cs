using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KLC_Finch.Modules {
    public class ServicesData {

        public ObservableCollection<ServiceValue> ListService { get; set; }

        public ServicesData() {
            ListService = new ObservableCollection<ServiceValue>();
        }

        public void ServicesClear() {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListService.Clear();
            });
        }

        public void ServicesAdd(ServiceValue sv) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListService.Add(sv);
            });
        }

    }
}
