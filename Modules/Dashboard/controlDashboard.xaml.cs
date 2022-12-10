using KLC;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for controlDashboard.xaml
    /// </summary>
    public partial class controlDashboard : UserControl {

        private Dashboard moduleDashboard;
        private StaticImage moduleStaticImage;

        public enum Badge {
            Blank,
            Note,
            FlagRed,
            FlagBlue,
            FlagGreen,
            FlagYellow,
            Recycle,
            Clock,
            Location,
            StarYellow,
            StarGreen,
            StarBlue,
            StarRed,
            UsePrivate,
            MagnifyGlass,
            PhoneOrange,
            PhoneBlue,
            Documentation,
            FilingCabinetBlue,
            UnknownArrowGreen,
            Envelope,
            PencilOrange,
            PencilBlue,
            SpeechBubble,
            PersonYellow
        };

        public controlDashboard() {
            InitializeComponent();
        }

        public void UpdateDisplayData() {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;

            txtUtilisationRAM.Text = "RAM: " + session.agent.RAMinGB + " GB";
            DisplayRCNotify(session.RCNotify);
            DisplayRCNotify(session.RCNotify);
            DisplayMachineNote(session.agent.MachineShowToolTip, session.agent.MachineNote, session.agent.MachineNoteLink);
        }

        public void btnDashboardStartData_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) { //Intentionally different
                btnDashboardStartData.IsEnabled = false;

                moduleDashboard = new Dashboard(session, txtDashboard, stackDisks, txtUtilisationRAM, txtUtilisationCPU, progressCPU, progressRAM);
                session.ModuleDashboard = moduleDashboard;
            }
        }

        private void btnDashboardGetCpuRam_Click(object sender, RoutedEventArgs e) {
            if (moduleDashboard != null) {
                moduleDashboard.GetCpuRam();
                //moduleDashboard.GetTopEvents();
                //moduleDashboard.GetTopProcesses();
            }
        }

        private void btnDashboardGetVolumes_Click(object sender, RoutedEventArgs e) {
            if (moduleDashboard != null)
                moduleDashboard.GetVolumes();
        }

        public void btnStaticImageStart_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) { //Intentionally different
                btnStaticImageStart.IsEnabled = false;
                lblStaticImage.Visibility = Visibility.Collapsed;

                moduleStaticImage = new StaticImage(session, imgScreenPreview);
                session.ModuleStaticImage = moduleStaticImage;
            }
        }

        private void btnStaticImageRefresh_Click(object sender, RoutedEventArgs e) {
            if (moduleStaticImage == null)
                return;

            moduleStaticImage.RequestRefresh();
        }

        private void btnStaticImageRefreshFull_Click(object sender, RoutedEventArgs e) {
            if (moduleStaticImage == null)
                return;

            moduleStaticImage.RequestRefreshFull();
        }

        public void DisplayRCNotify(LibKaseya.Enums.NotifyApproval policy) {
            switch(policy) {
                case LibKaseya.Enums.NotifyApproval.None:
                    txtRCNotify.Visibility = Visibility.Collapsed;
                    break;
                case LibKaseya.Enums.NotifyApproval.NotifyOnly:
                    txtRCNotify.Text = "Notification prompt only.";
                    break;
                case LibKaseya.Enums.NotifyApproval.ApproveAllowIfNoUser:
                    txtRCNotify.Text = "Approve prompt - allow if no one logged in.";
                    break;
                case LibKaseya.Enums.NotifyApproval.ApproveDenyIfNoUser:
                    txtRCNotify.Text = "Approve prompt - denied if no one logged in.";
                    break;
                default:
                    txtRCNotify.Text = "Unknown RC notify policy: " + policy;
                    break;
            }
        }

        //session.agent.MachineShowToolTip, session.agent.MachineNote, session.agent.MachineNoteLink
        public void DisplayMachineNote(int machineShowToolTip, string machineNote, string machineNoteLink=null) {
            if (machineNote == null)
                return;

            if (machineShowToolTip == 0 && machineNote.Length == 0) {
                txtSpecialInstructions.Visibility = txtMachineNote.Visibility = Visibility.Collapsed;
                return;
            }

            if (machineShowToolTip > 0) {
                if (Enum.IsDefined(typeof(Badge), machineShowToolTip))
                    txtSpecialInstructions.Text += " (" + Enum.GetName(typeof(Badge), machineShowToolTip) + ")";
                else
                    txtSpecialInstructions.Text += " (" + machineShowToolTip + ")";
            }

            if (machineNoteLink != null) {
                txtMachineNoteLink.NavigateUri = new Uri(machineNoteLink);
                txtMachineNoteLinkText.Text = machineNoteLink;
                txtMachineNoteText.Text = machineNote;
            } else {
                txtMachineNoteLinkText.Text = string.Empty;
                txtMachineNoteText.Text = machineNote;
            }

        }

        private void txtMachineNoteLink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
            Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
        }

        private void btnStaticImageDumpLayout_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetDataObject(moduleStaticImage.DumpScreens());
        }

        public void UpdateTimer()
        {
            if (moduleDashboard != null)
                moduleDashboard.UpdateTimer();
        }

    }
}
