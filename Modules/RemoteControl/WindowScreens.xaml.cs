using LibKaseya;
using Newtonsoft.Json;
using NTR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
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
    /// Interaction logic for WindowScreens.xaml
    /// </summary>
    public partial class WindowScreens : Window {

        private int offsetX, offsetY;
        private System.Drawing.Rectangle virtualCanvas, virtualView;
        //private List<RCScreen> listScreen;

        private WindowViewerV3 viewer;
        private string infoStart;

        public DateTime TimeDeactivated;

        public WindowScreens(Agent.OSProfile endpointOS = Agent.OSProfile.Other) {
            InitializeComponent();
            rcBorderExample.Visibility = Visibility.Hidden;
            
            if (endpointOS == Agent.OSProfile.Mac)
                toolUpdate.Visibility = Visibility.Collapsed;
        }

        private void Window_Activated(object sender, EventArgs e) {
            if (viewer != null && Owner.WindowState == WindowState.Maximized) {
                Point point = viewer.placeholder.TransformToAncestor(viewer).Transform(new Point(0, 0));

                IntPtr handle = new System.Windows.Interop.WindowInteropHelper(Owner).Handle;
                System.Windows.Forms.Screen screen = System.Windows.Forms.Screen.FromHandle(handle);

                this.Left = screen.WorkingArea.Left;
                this.Top = screen.WorkingArea.Top + SystemParameters.CaptionHeight + point.Y + 1;
            } else {
                this.Left = Owner.Left + 8;
                this.Top = Owner.Top + 66;
            }

            Render();
        }

        private void Window_Deactivated(object sender, EventArgs e) {
            this.Visibility = Visibility.Hidden;
            TimeDeactivated = DateTime.Now;
        }

        public void SetCanvas(int virtualX, int virtualY, int virtualWidth, int virtualHeight) {
            offsetX = virtualX;
            offsetY = virtualY;

            virtualCanvas = new System.Drawing.Rectangle(virtualX, virtualY, Math.Abs(virtualX) + virtualWidth, Math.Abs(virtualY) + virtualHeight);
            virtualView = virtualCanvas;

            if (this.Visibility == Visibility.Visible) {
                Dispatcher.Invoke((Action)delegate {
                    Render();
                });
            }
        }

        private void Render() {
            if (viewer == null && Owner != null) {
                viewer = (WindowViewerV3)Owner;
                Window_Activated(null, null);
            }
            if (viewer == null)
                return;

            List<RCScreen> listScreen = viewer.GetListScreen();
            if (listScreen == null)
                return;

            rcCanvas.Width = virtualCanvas.Width;
            rcCanvas.Height = virtualCanvas.Height;
            rcCanvas.Children.Clear();

            foreach (RCScreen screen in listScreen) {

                Border r = new Border();
                TextBlock t = new TextBlock();

                r.Height = screen.rect.Height;
                r.Width = screen.rect.Width;
                r.Tag = screen.screen_id;
                r.ToolTip = screen.ToString();
                r.MouseDown += Border_MouseDown;

                int screenNum = rcCanvas.Children.Count;
                byte grey = (byte)(255 - (screenNum * 20));
                r.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(grey, grey, grey));
                if (grey < 101)
                    t.Foreground = new SolidColorBrush(System.Windows.Media.Colors.White);

                /*
                if (screenNum == 0)
                    r.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 218, 3)); //Yellow
                else if (screenNum == 1)
                    r.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 165, 50)); //Orange
                else if (screenNum == 2)
                    r.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(53, 166, 170)); //Teal
                else if (screenNum == 3)
                    r.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 108, 167)); //Pink
                else if (screenNum == 4)
                    r.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(57, 54, 122)); //Purple
                else
                    r.Background = new SolidColorBrush(System.Windows.Media.Colors.White);
                */

                t.Text = string.Format("{0}\r\n{1}", screen.screen_name, screen.rect.Width + " x " + screen.rect.Height, "at " + screen.rect.X + ", " + screen.rect.Y);
                t.TextAlignment = TextAlignment.Center;
                t.VerticalAlignment = VerticalAlignment.Center;
                t.FontSize = 150;
                r.Child = t;

                //listCanvasRectangle.Add(r);
                rcCanvas.Children.Add(r);
                Canvas.SetLeft(r, screen.rect.X - offsetX);
                Canvas.SetTop(r, screen.rect.Y - offsetY);
            }
        }

        private void Border_MouseDown(object sender, MouseButtonEventArgs e) {
            string id = (string)((Border)sender).Tag;
            viewer.SetScreen(id);
        }

        public void SetVirtual(int virtualX, int virtualY, int virtualWidth, int virtualHeight) {
            //virtualView = new System.Drawing.Rectangle(virtualX, virtualY, virtualWidth, virtualHeight);

            //virtualRequireViewportUpdate = true;

            rcViewbox.Stretch = Stretch.None;
        }

        private void ToolUpdate_Click(object sender, RoutedEventArgs e) {
            viewer.UpdateScreenLayoutHack();
        }

        private void ToolReflow_Click(object sender, RoutedEventArgs e) {
            List<RCScreen> listScreen = viewer.GetListScreen();
            if (listScreen == null)
                return;

            RCScreen center = listScreen.Find(x => x.rectOrg.X == 0);
            if (center == null)
                return;

            foreach(RCScreen screen in listScreen) {
                if (screen != center) {
                    if (screen.rectOrg.X > 0) {
                        screen.rect.X = center.rect.Right;
                    } else if (screen.rectOrg.X < 0) {
                        screen.rect.X = -screen.rect.Width;
                    }
                }
            }

            viewer.UpdateScreenLayoutReflow();
        }

        private void toolReset_Click(object sender, RoutedEventArgs e) {
            List<RCScreen> listScreen = viewer.GetListScreen();
            if (listScreen == null)
                return;

            foreach (RCScreen screen in listScreen) {
                screen.rect = screen.rectOrg;
            }

            viewer.UpdateScreenLayoutReflow();
        }

        private void ToolDump_Click(object sender, RoutedEventArgs e) {
            List<RCScreen> listScreen = viewer.GetListScreen();

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Start info:");
            sb.AppendLine(infoStart);
            sb.AppendLine("");
            sb.AppendLine("Current info:");
            bool more = false;
            foreach (RCScreen screen in listScreen) {
                if (more)
                    sb.Append(",");

                sb.AppendLine('{' + string.Format("\"screen_height\":{0},\"screen_id\":{1},\"screen_name\":\"{2}\",\"screen_width\":{3},\"screen_x\":{4},\"screen_y\":{5}", screen.rect.Height, screen.screen_id, screen.screen_name.Replace("\\", "\\\\"), screen.rect.Width, screen.rect.X, screen.rect.Y) + '}');

                more = true;
            }
            /*
            sb.AppendLine("");
            sb.AppendLine("Current info (Fixed)");
            foreach (RCScreen screen in listScreen) {
                sb.AppendLine('{' + string.Format("\"screen_height\":{0},\"screen_id\":{1},\"screen_name\":\"{2}\",\"screen_width\":{3},\"screen_x\":{4},\"screen_y\":{5}", screen.rect.Height, screen.screen_id, screen.screen_name, screen.rect.Width, screen.rect.X, screen.rect.Y) + '}');
            }
            */

            Clipboard.SetDataObject(sb.ToString());
        }

        public void UpdateStartScreens(string info) {
            infoStart = info;
        }

    }
}
