using System.Windows.Controls;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for controlAsset.xaml
    /// </summary>
    public partial class controlAsset : UserControl {

        private Modules.AssetData assetData;

        public controlAsset() {
            assetData = new Modules.AssetData();
            this.DataContext = assetData;
            InitializeComponent();
        }
    }
}
