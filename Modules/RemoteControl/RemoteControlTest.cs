using System;
using System.Drawing;
using System.Threading;
using System.Windows.Input;

namespace KLC_Finch
{
    public class RemoteControlTest : IRemoteControl
    {

        public WindowViewerV3 Viewer;
        //public bool UseYUVShader { get { return false; } set { } }
        public DecodeMode DecodeMode { get; set; }
        public bool IsMac { get; set; }
		public Modules.RemoteControl.Transfer.RCFile Files { get; private set; }
		
		public bool IsPrivate
        {
            get
            {
                return false;
            }
        }

        private string screenStr;
        private Thread threadTest;
        private static CancellationTokenSource threadCTokenSource;
        private bool retina;

        private static Color[] colors = new Color[] { //The BIT.TRIP colours!
            Color.FromArgb(251, 218, 3), //Yellow
            Color.FromArgb(255, 165, 50), //Orange
            Color.FromArgb(53, 166, 170), //Teal
            Color.FromArgb(220, 108, 167), //Pink
            Color.FromArgb(57, 54, 122) //Purple
        };
        private static byte[,] colorsYUV = new byte[5, 3] {
            { 203, 14, 161 }, //Yellow
            { 178, 55, 182 }, //Orange
            { 132, 149, 71 }, //Teal
            { 148, 138, 179 }, //Pink
            { 62, 161, 123 } //Purple
        };
        private int colorPos;

        public void LoopStart(WindowViewerV3 viewer)
        {
            if (threadTest != null)
                LoopStop();

            Viewer = viewer;
            threadCTokenSource = new CancellationTokenSource();
            threadTest = new Thread(() => {
                Loop();
            });
            threadTest.Start();
        }

        public void LoopStop()
        {
            if (threadTest != null)
            {
                threadCTokenSource.Cancel();
                threadTest.Join(1000);
            }
        }

        private void Loop()
        {
            Viewer.ClearApproval();
            while (!threadCTokenSource.Token.IsCancellationRequested && Viewer.IsVisible)
            {
                Thread.Sleep(500);

                NTR.RCScreen screen = Viewer.GetCurrentScreen();
                if (screen == null)
                    continue;

                int width = screen.rectOrg.Width;
                int height = screen.rectOrg.Height;
                if (retina)
                {
                    width *= 2;
                    height *= 2;
                }

                if (DecodeMode == DecodeMode.BitmapRGB)
                {
                    Bitmap bTest = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                    using (Graphics g = Graphics.FromImage(bTest)) { g.Clear(colors[colorPos]); }
                    Viewer.LoadTexture(bTest.Width, bTest.Height, bTest);
                    bTest.Dispose();
                }
                else
                {
                    int sizeY = width * height;
                    int sizeUV = width * height / 4;
                    byte[] yuv = new byte[sizeY + sizeUV + sizeUV];
                    for (int i = 0; i < sizeY; i++)
                    {
                        yuv[i] = colorsYUV[colorPos, 0];
                        yuv[i + sizeUV] = colorsYUV[colorPos, 1];
                        yuv[i + sizeUV + sizeUV] = colorsYUV[colorPos, 2];
                    }
                    Viewer.LoadTextureRaw(yuv, width, height, width);
                }

                GC.Collect();

                colorPos++;
                if (colorPos >= colors.Length)
                    colorPos = 0;
            }
        }

        public void CaptureNextScreen()
        {
            //throw new NotImplementedException();
        }

        public void UpdateScreens(string jsonstr)
        {
            screenStr = jsonstr;
        }

        public void ChangeScreen(string screen_id, int clientH, int clientW, int downscale)
        {
            //Console.WriteLine("ChangeScreen: " + screen_id);
            //screenCurrent = screenList.Find(x => x.screen_id == screen_id);
        }

        public void ChangeTSSession(string session_id)
        {
            //throw new NotImplementedException();
        }

        public void Disconnect(string sessionId)
        {
            LoopStop();

            if (Viewer != null)
                Viewer.NotifySocketClosed(sessionId);
        }

        public void Reconnect()
        {
            LoopStart(Viewer);
        }

        public void SendAutotype(string text)
        {
            //throw new NotImplementedException();
        }

        public void SendPasteClipboard(string clipboard)
        {
            //throw new NotImplementedException();
        }

        public void SendClipboard(string clipboard)
        {
            //throw new NotImplementedException();
        }

        public void SendKeyDown(int javascriptKeyCode, int uSBKeyCode)
        {
            //throw new NotImplementedException();
        }

        public void SendKeyUp(int javascriptKeyCode, int uSBKeyCode)
        {
            //throw new NotImplementedException();
        }

        public void SendMouseDown(MouseButton changedButton)
        {
            //throw new NotImplementedException();
        }

        public void SendMouseDown(System.Windows.Forms.MouseButtons changedButton)
        {
            //throw new NotImplementedException();
        }

        public void SendMousePosition(int x, int y)
        {
            //throw new NotImplementedException();
        }

        public void SendMouseUp(MouseButton changedButton)
        {
            //throw new NotImplementedException();
        }

        public void SendMouseUp(System.Windows.Forms.MouseButtons changedButton)
        {
            //throw new NotImplementedException();
        }

        public void SendMouseWheel(int delta)
        {
            //throw new NotImplementedException();
        }

        public void SetRetina(bool isChecked)
        {
            retina = isChecked;
        }

        public void SendPanicKeyRelease()
        {
            //throw new NotImplementedException();
        }

        public void SendSecureAttentionSequence()
        {
            //throw new NotImplementedException();
        }

        public void FileTransferUpload(string[] files)
        {
            //throw new NotImplementedException();
        }

        public void FileTransferUploadCancel()
        {
            //throw new NotImplementedException();
        }

        public void FileTransferDownload()
        {
            //throw new NotImplementedException();
        }

        public void FileTransferDownloadCancel()
        {
            //throw new NotImplementedException();
        }

        public void ShowCursor(bool enabled)
        {
            //throw new NotImplementedException();
        }

        public void SendBlackScreenBlockInput(bool blackOutScreen, bool blockMouseKB)
        {
            //throw new NotImplementedException();
        }

        public void UpdateScreensHack()
        {
            //throw new NotImplementedException();
        }

    }
}
