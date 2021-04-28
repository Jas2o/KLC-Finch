using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace KLC_Finch.Modules {
    public class ProcessesData {

        public ObservableCollection<ProcessValue> ListProcess { get; set; }

        public ProcessesData() {
            ListProcess = new ObservableCollection<ProcessValue>();
        }

        public void ProcessesClear() {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListProcess.Clear();
            });
        }

        public void ProcessesAdd(ProcessValue pv) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListProcess.Add(pv);
            });
        }

    }
}
