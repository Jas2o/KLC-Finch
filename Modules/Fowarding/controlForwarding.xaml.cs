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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KLC_Finch
{
    /// <summary>
    /// Interaction logic for controlForwarding.xaml
    /// </summary>
    public partial class controlForwarding : UserControl
    {
        KLC.LiveConnectSession Session;

        public controlForwarding()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if(Session == null)
                Session = ((WindowAlternative)Window.GetWindow(this)).session;

            if (txtIPAddress.Text.Length == 0 && Session != null && Session.agent != null)
                txtIPAddress.Text = Session.agent.NetIPAddress;
        }

        private void btnForwardingStart_Click(object sender, RoutedEventArgs e)
        {
            if (Session == null)
                return;

            try
            {
                Session.ModuleForwarding = new Forwarding(Session, txtIPAddress.Text.Trim(), int.Parse(txtPort.Text), txtAccess, lblStatus);

                btnForwardingStart.IsEnabled = false;
                btnForwardingEnd.IsEnabled = true;
            } catch(Exception) {
            }
        }

        private void btnForwardingEnd_Click(object sender, RoutedEventArgs e)
        {
            if (Session == null || Session.ModuleForwarding == null)
                return;

            Session.ModuleForwarding.Close();
            Session.ModuleForwarding = null;

            Session.Callback(LibKaseya.Enums.EPStatus.NativeRDPEnded);
            txtAccess.Text = "";
            btnForwardingEnd.IsEnabled = false;
            btnForwardingStart.IsEnabled = true;
        }
    }
}
