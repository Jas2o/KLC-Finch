using System;
using System.Text;
using System.Windows.Controls;
using System.Windows.Media;

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for controlDisk.xaml
    /// </summary>
    public partial class controlDisk : UserControl {
        public controlDisk() {
            InitializeComponent();
        }

        public controlDisk(string label, long total, long free) {
            InitializeComponent();

            StringBuilder totalSb = new StringBuilder(32);
            FormatKbSizeConverter.StrFormatByteSizeW(total, totalSb, totalSb.Capacity);

            StringBuilder freeSb = new StringBuilder(32);
            FormatKbSizeConverter.StrFormatByteSizeW(free, freeSb, freeSb.Capacity);
            int freePercent = (int)Math.Floor((free / (double)total) * 100);

            StringBuilder usedSb = new StringBuilder(32);
            long used = total - free;
            FormatKbSizeConverter.StrFormatByteSizeW(used, usedSb, usedSb.Capacity);
            int usedPercent = (int)Math.Ceiling((used / (double)total) * 100);

            txtLabelFreeOfCapacity.Content = string.Format("{0} {1} ({2}%) free of {3}", label, freeSb.ToString(), freePercent, totalSb.ToString());
            txtUsed.Text = string.Format("{0} ({1}%) used", usedSb.ToString(), usedPercent);

            progressBar.Value = usedPercent;

            if(usedPercent >= 95)
                progressBar.Foreground = new SolidColorBrush(Colors.Red);
            else if(usedPercent >= 90)
                progressBar.Foreground = new SolidColorBrush(Colors.Goldenrod);
        }
    }
}
