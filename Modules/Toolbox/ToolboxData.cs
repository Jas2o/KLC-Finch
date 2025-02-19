using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace KLC_Finch {
    public class ToolboxData : INotifyPropertyChanged {

        public ObservableCollection<ToolboxValue> ListToolbox { get; set; }
        private string _status;
        public string Status {
            get {
                return _status;
            }
            set {
                App.Current.Dispatcher.Invoke((Action)delegate {
                    _status = value;
                });
                this.NotifyPropertyChanged("Status");
            }
        }

        public ToolboxData() {
            ListToolbox = new ObservableCollection<ToolboxValue>();
            Status = "";
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Clear() {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListToolbox.Clear();
            });
        }

        public void Add(ToolboxValue tv) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListToolbox.Add(tv);
            });
        }
    }
}
