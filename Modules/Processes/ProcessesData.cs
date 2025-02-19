using System;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace KLC_Finch.Modules {
    public class ProcessesData : INotifyPropertyChanged {

        public ObservableCollection<ProcessValue> ListProcess { get; set; }

        public ProcessesData() {
            ListProcess = new ObservableCollection<ProcessValue>();
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

    }
}
