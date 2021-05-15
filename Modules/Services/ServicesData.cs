using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

namespace KLC_Finch.Modules {
    public class ServicesData {

        public ObservableCollection<ServiceValue> ListService { get; set; }
        private ListCollectionView _listCollectionView;

        public ServicesData() {
            ListService = new ObservableCollection<ServiceValue>();

            _listCollectionView = CollectionViewSource.GetDefaultView(ListService) as ListCollectionView;
            if (_listCollectionView != null) {
                _listCollectionView.IsLiveSorting = true;
                _listCollectionView.CustomSort = new CaseInsensitiveComparer(CultureInfo.InvariantCulture);
            }
        }

        public void ServicesClear() {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ListService.Clear();
            });
        }

        public void ServicesAdd(ServiceValue sv) {
            App.Current.Dispatcher.Invoke((Action)delegate {
                ServiceValue match = ListService.FirstOrDefault(x => x.ServiceName == sv.ServiceName);
                if(match != null)
                    ListService.Remove(match);

                ListService.Add(sv);
            });
        }

    }
}
