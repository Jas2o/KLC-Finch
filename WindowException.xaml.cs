using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for WindowException.xaml
    /// </summary>
    public partial class WindowException : Window {

        public Exception Exception { get; private set; }
        public string ExceptionSource { get; private set; }
        public string DetailsText { get; private set; }

        public WindowException() {
            InitializeComponent();
        }

        public WindowException(Exception ex, string source) { //, bool allowContinue
            Exception = ex;
            ExceptionSource = source;
            DetailsText = ExceptionSource + "\r\n" + ex.ToString();
            this.DataContext = this;

            InitializeComponent();

            this.Title = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " Exception";
            //btnContinue.Visibility = (allowContinue ? Visibility.Visible : Visibility.Collapsed);
        }

        public WindowException(string error, string source) { //, bool allowContinue
            Exception = new Exception(source);
            ExceptionSource = source;
            DetailsText = error;
            this.DataContext = this;

            InitializeComponent();

            this.Title = System.Reflection.Assembly.GetExecutingAssembly().GetName().Name + " Exception";
            //btnContinue.Visibility = (allowContinue ? Visibility.Visible : Visibility.Collapsed);
        }

        private void OnExitAppClick(object sender, RoutedEventArgs e) {
            Environment.Exit(0);
        }

        private void OnContinueAppClick(object sender, RoutedEventArgs e) {
            Close();
        }
    }
}
