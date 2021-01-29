//Try: https://stackoverflow.com/questions/38148400/clipboard-monitor

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace KLC
{
    public class ClipBoardMonitor : NativeWindow
    {
        //Based on: https://stackoverflow.com/questions/31258905/how-to-monitor-clipboard-changes-in-c-sharp-to-capture-only-hyperlinks

        //Usage:
        //cbm = new ClipBoardMonitor();
        //cbm.OnUpdate += UpdateClipboard;

        private const int WM_DRAWCLIPBOARD = 0x308;

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);
        private IntPtr NextClipBoardViewerHandle;

        public event EventHandler OnUpdate;

        public ClipBoardMonitor()
        {
            OnUpdate = delegate { };
            this.CreateHandle(new CreateParams());
            this.NextClipBoardViewerHandle = SetClipboardViewer(this.Handle);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_DRAWCLIPBOARD:
                    OnUpdate(this, EventArgs.Empty);
                    break;
            }

            base.WndProc(ref m);
        }

    }
}
