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

namespace KLC_Finch
{
    /// <summary>
    /// Interaction logic for WindowStreamQuality.xaml
    /// </summary>
    public partial class WindowStreamQuality : Window
    {
        private WindowViewerV3.QualityCallback Callback;
        private int ResultDownscaleLimit = 1;
        private int ResultWidth = 0;
        private int ResultHeight = 0;

        public WindowStreamQuality(WindowViewerV3.QualityCallback callback, int downscale = 1, int width = 0, int height = 0)
        {
            InitializeComponent();
            Callback = callback;

            if(downscale == 2)
                radioDownscale2.IsChecked = true;
            else if (downscale == 4)
                radioDownscale4.IsChecked = true;
            else if (downscale == 8)
                radioDownscale8.IsChecked = true;
            else
                radioDownscale1.IsChecked = true;

            if(height == 480)
                radioSize480.IsChecked = true;
            else if (height == 600)
                radioSize600.IsChecked = true;
            else if (height == 768)
                radioSize768.IsChecked = true;
            else if (height == 576)
                radioSize576.IsChecked = true;
            else if (height == 720)
                radioSize720.IsChecked = true;
            else if (height == 900)
                radioSize900.IsChecked = true;
            else
                radioSizeWindow.IsChecked = true;
        }

        private void btnQualityChange(object sender, RoutedEventArgs e)
        {
            if (radioDownscale2.IsChecked == true)
                ResultDownscaleLimit = 2;
            else if (radioDownscale4.IsChecked == true)
                ResultDownscaleLimit = 4;
            else if (radioDownscale8.IsChecked == true)
                ResultDownscaleLimit = 8;
            else
                ResultDownscaleLimit = 1;

            //--

            if (radioSize480.IsChecked == true)
            {
                ResultWidth = 640;
                ResultHeight = 480;
            }
            else if (radioSize600.IsChecked == true)
            {
                ResultWidth = 800;
                ResultHeight = 600;
            }
            else if (radioSize768.IsChecked == true)
            {
                ResultWidth = 1024;
                ResultHeight = 768;
            }
            else if (radioSize576.IsChecked == true)
            {
                ResultWidth = 720;
                ResultHeight = 576;
            }
            else if (radioSize720.IsChecked == true)
            {
                ResultWidth = 1280;
                ResultHeight = 720;
            }
            else if (radioSize900.IsChecked == true)
            {
                ResultWidth = 1600;
                ResultHeight = 900;
            }
            else
            {
                ResultWidth = 0;
                ResultHeight = 0;
            }

            Callback.Invoke(ResultDownscaleLimit, ResultWidth, ResultHeight);
        }
    }
}
