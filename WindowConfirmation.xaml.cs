using System.Windows;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for WindowException.xaml
    /// </summary>
    public partial class WindowConfirmation : Window {

        public string Label { get; private set; }
        public string Payload { get; private set; }

        public WindowConfirmation() {
            InitializeComponent();
        }

        public WindowConfirmation(string label, string payload) {
            Label = label;
            Payload = payload;
            this.DataContext = this;
            
            InitializeComponent();

            this.Title = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " Confirmation";
        }

        private void OnContinueAppClick(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            Close();
        }
    }
}
