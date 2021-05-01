﻿using System;
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
            UNEXPECTED,
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

        public void btnDashboardStartData_Click(object sender, RoutedEventArgs e) {
            KLC.LiveConnectSession session = ((WindowAlternative)Window.GetWindow(this)).session;
            if (session != null) {
                btnDashboardStartData.IsEnabled = false;

                moduleDashboard = new Dashboard(session, txtDashboard, stackDisks);
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
            if (session != null) {
                btnStaticImageStart.IsEnabled = false;

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

        public void DisplayRCNotify(int policy) {
            if (policy == 1)
                txtRCNotify.Visibility = Visibility.Collapsed;
            else if (policy == 2)
                txtRCNotify.Text = "Notification prompt only.";
            else if (policy == 3)
                txtRCNotify.Text = "Approve prompt - allow if no one logged in.";
            else if (policy == 4)
                txtRCNotify.Text = "Approve prompt - denied if no one logged in.";
            else
                txtRCNotify.Text = "Unknown RC notify policy: " + policy;
        }

        public void DisplayMachineNote(string machineNote, int machineShowToolTip) {
            if (machineNote.Length == 0) {
                txtSpecialInstructions.Visibility = txtMachineNote.Visibility = Visibility.Collapsed;
                return;
            }

            if (machineShowToolTip > 0) {
                if (Enum.IsDefined(typeof(Badge), machineShowToolTip))
                    txtSpecialInstructions.Text += " (" + Enum.GetName(typeof(Badge), machineShowToolTip) + ")";
                else
                    txtSpecialInstructions.Text += " (" + machineShowToolTip + ")";
            }

            string link = string.Empty;
            string[] links = machineNote.Split("\t\n ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Where(s => s.StartsWith("http://") || s.StartsWith("https://") || s.StartsWith("www.")).ToArray();

            if (links != null && links.Length > 0) {
                link = links[0];
                txtMachineNoteLink.NavigateUri = new Uri(link);
                txtMachineNoteLinkText.Text = link;
                txtMachineNoteText.Text = machineNote.Replace(link, "").Trim();
            } else {
                txtMachineNoteLinkText.Text = string.Empty;
                txtMachineNoteText.Text = machineNote.Trim();
            }

        }

        private void txtMachineNoteLink_RequestNavigate(object sender, RequestNavigateEventArgs e) {
            Process.Start(new ProcessStartInfo(e.Uri.ToString()) { UseShellExecute = true });
        }

    }
}
