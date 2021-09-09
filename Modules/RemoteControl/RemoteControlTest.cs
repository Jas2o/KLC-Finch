using System;
using System.Drawing;
using System.Threading;
using System.Windows.Input;

namespace KLC_Finch {
    public class RemoteControlTest : IRemoteControl {

        public WindowViewer Viewer;
        //public bool UseYUVShader { get { return false; } set { } }
        public DecodeMode DecodeMode { get { return DecodeMode.BitmapRGB; } set { } }

        private string screenStr;
        private Thread threadTest;

        private readonly Color[] colors = new Color[] { //The BIT.TRIP colours!
            Color.FromArgb(251, 218, 3), //Yellow
            Color.FromArgb(255, 165, 50), //Orange
            Color.FromArgb(53, 166, 170), //Teal
            Color.FromArgb(220, 108, 167), //Pink
            Color.FromArgb(57, 54, 122) //Purple
        };
        private int colorPos;

        public void LoopStart(WindowViewer viewer) {
            if (threadTest != null)
                LoopStop();

            Viewer = viewer;
            threadTest = new Thread(() => {
                Loop();
            });
            threadTest.Start();
        }

        public void LoopStop() {
            if (threadTest != null) {
                threadTest.Abort();
                threadTest.Join();
            }
        }

        private void Loop() {
            Viewer.ClearApproval();

            while (Viewer.IsVisible) {
                Thread.Sleep(500);

                NTR.RCScreen screen = Viewer.GetCurrentScreen();
                if (screen == null)
                    continue;
                
                Bitmap bTest = new Bitmap(screen.rect.Width, screen.rect.Height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using (Graphics g = Graphics.FromImage(bTest)) { g.Clear(colors[colorPos]); }
                Viewer.LoadTexture(bTest.Width, bTest.Height, bTest);
                bTest.Dispose();

                colorPos++;
                if (colorPos >= colors.Length)
                    colorPos = 0;
            }
        }

        public void CaptureNextScreen() {
            //throw new NotImplementedException();
        }

        public void UpdateScreens(string jsonstr) {
            screenStr = jsonstr;
        }

        public void ChangeScreen(string screen_id) {
            //Console.WriteLine("ChangeScreen: " + screen_id);
            //screenCurrent = screenList.Find(x => x.screen_id == screen_id);
        }

        public void ChangeTSSession(string session_id) {
            //throw new NotImplementedException();
        }

        public void Disconnect(string sessionId) {
            LoopStop();
        }

        public void Reconnect() {
            LoopStart(Viewer);
        }

        public void SendAutotype(string text) {
            //throw new NotImplementedException();
        }

        public void SendPasteClipboard(string clipboard) {
            //throw new NotImplementedException();
        }

        public void SendClipboard(string clipboard) {
            //throw new NotImplementedException();
        }

        public void SendKeyDown(int javascriptKeyCode, int uSBKeyCode) {
            //throw new NotImplementedException();
        }

        public void SendKeyUp(int javascriptKeyCode, int uSBKeyCode) {
            //throw new NotImplementedException();
        }

        public void SendMouseDown(MouseButton changedButton) {
            //throw new NotImplementedException();
        }

        public void SendMouseDown(System.Windows.Forms.MouseButtons changedButton) {
            //throw new NotImplementedException();
        }

        public void SendMousePosition(int x, int y) {
            //throw new NotImplementedException();
        }

        public void SendMouseUp(MouseButton changedButton) {
            //throw new NotImplementedException();
        }

        public void SendMouseUp(System.Windows.Forms.MouseButtons changedButton) {
            //throw new NotImplementedException();
        }

        public void SendMouseWheel(int delta) {
            //throw new NotImplementedException();
        }

        public void SendPanicKeyRelease() {
            //throw new NotImplementedException();
        }

        public void SendSecureAttentionSequence() {
            //throw new NotImplementedException();
        }

        public void UploadDrop(string v, Progress<int> progress) {
            //throw new NotImplementedException();
        }

        public void ShowCursor(bool enabled) {
            //throw new NotImplementedException();
        }

        public void SendBlackScreenBlockInput(bool blackOutScreen, bool blockMouseKB) {
            //throw new NotImplementedException();
        }
    }
}
