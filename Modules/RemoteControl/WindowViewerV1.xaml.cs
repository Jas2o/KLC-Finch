using KLC;
using LibKaseya;
using NTR;
using nucs.JsonSettings;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

//To look into regarding the changes to Key/Keys
//https://stackoverflow.com/questions/1153009/how-can-i-convert-system-windows-input-key-to-system-windows-forms-keys

namespace KLC_Finch {
    /// <summary>
    /// Interaction logic for WindowViewer.xaml
    /// </summary>
    public partial class WindowViewerV1 : Window {

        public Settings Settings;

        private bool virtualRequireViewportUpdate = false;
        private bool controlEnabled = false;
        
        private string clipboard = "";
        private KLC.ClipBoardMonitor clipboardMon;
        private bool socketAlive = false;
        //private bool reachedFirstConnect = false;
        private List<RCScreen> listScreen = new List<RCScreen>();
        private RCScreen currentScreen = null;

        private string sessionId;
        private RemoteControl rc;
        #region Copy
        int screenWidth = 0, screenHeight = 0, virtualWidth, virtualHeight, vpX, vpY;
        float targetAspectRatio;
        double scaleX, scaleY;

        private static Font arial = new Font("Arial", 42);
        Bitmap overlay2dMouse;
        Bitmap overlay2dKeyboard;
        Bitmap overlay2dDisconnected;
        byte[] textureOverlayDataMouse;
        byte[] textureOverlayDataKeyboard;
        byte[] textureOverlayDataDisconnected;
        bool overlayNewMouse;
        bool overlayNewKeyboard;
        int textureOverlay2dMouse;
        int textureOverlay2dKeyboard;
        int textureOverlay2dDisconnected;
        private const int overlayWidth = 1000;
        private const int overlayHeight = 100;

        Vector2[] vertBufferScreen, vertBufferMouse, vertBufferKeyboard, vertBufferDisconnected;
        int VBOScreen, VBOmouse, VBOkeyboard, VBOdisconnected;

        private int textureWidth;
        private int textureHeight;
        private byte[] textureData;
        private bool textureNew;
        int textureBottom;
        #endregion

        private bool keyDownWin;
        private bool autotypeAlwaysConfirmed;
        private bool windowActivatedMouseMove;

        enum ConnectionStatus {
            FirstConnectionAttempt,
            Connected,
            Disconnected
        }
        ConnectionStatus connectionStatus;

        public WindowViewerV1(RemoteControl rc, int virtualWidth = 1920, int virtualHeight = 1080) {
            InitializeComponent();

            string pathSettings = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\KLC-Finch-config.json";
            if (File.Exists(pathSettings))
                Settings = JsonSettings.Load<Settings>(pathSettings);
            else
                Settings = JsonSettings.Construct<Settings>(pathSettings);

            this.Width = Settings.RemoteControlWidth;
            this.Height = Settings.RemoteControlHeight;

            toolSettingStartControlEnabled.IsChecked = Settings.StartControlEnabled;
            toolDebugKeyboardMod.IsChecked = Settings.DisplayOverlayKeyboardMod;
            toolDebugKeyboardOther.IsChecked = Settings.DisplayOverlayKeyboardOther;
            toolDebugMouse.IsChecked = Settings.DisplayOverlayMouse;
            // This repetition needs to be fixed
            if (Settings.ClipboardSyncEnabled)
                toolClipboardSync.Header = "Clipboard (Synced)";
            else
                toolClipboardSync.Header = "Clipboard (Receive Only)";

            this.rc = rc;
            socketAlive = false;
            connectionStatus = ConnectionStatus.FirstConnectionAttempt;

            SetVirtual(virtualWidth, virtualHeight);

            clipboardMon = new ClipBoardMonitor();
            clipboardMon.OnUpdate += SyncClipboard;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            clipboardMon.OnUpdate -= SyncClipboard;

            if (rc != null)
                rc.Disconnect(sessionId);

            if (App.alternative != null && App.alternative.Visibility != Visibility.Visible)
                Environment.Exit(0);
        }

        public void SetTitle(string title) {
            this.Title = title;
        }

        public void SetSessionID(string sessionId) {
            this.sessionId = sessionId;

            if (sessionId != null) {
                socketAlive = true;
                connectionStatus = ConnectionStatus.Connected;
            }
        }

        public void SetVirtual(int virtualWidth, int virtualHeight) {
            this.virtualWidth = virtualWidth;
            this.virtualHeight = virtualHeight;

            virtualRequireViewportUpdate = true;
        }

        private void RefreshVirtual() {
            vertBufferScreen = new Vector2[8] {
                new Vector2(0,virtualHeight), new Vector2(0, 1),
                new Vector2(virtualWidth,virtualHeight), new Vector2(1, 1),
                new Vector2(virtualWidth,0), new Vector2(1, 0),
                new Vector2(0,0), new Vector2(0, 0)
            };

            vertBufferMouse = new Vector2[8] {
                new Vector2(virtualWidth - overlayWidth,overlayHeight), new Vector2(0, 1),
                new Vector2(virtualWidth,overlayHeight), new Vector2(1, 1),
                new Vector2(virtualWidth,0), new Vector2(1, 0),
                new Vector2(virtualWidth - overlayWidth,0), new Vector2(0, 0)
            };

            int leftDisconnected = (virtualWidth - 400) / 2;
            int topDisconnected = (virtualHeight - overlayHeight) / 2;
            vertBufferDisconnected = new Vector2[8] {
                new Vector2(leftDisconnected, topDisconnected + overlayHeight), new Vector2(0, 1),
                new Vector2(leftDisconnected + 400, topDisconnected + overlayHeight), new Vector2(1, 1),
                new Vector2(leftDisconnected + 400, topDisconnected), new Vector2(1, 0),
                new Vector2(leftDisconnected, topDisconnected), new Vector2(0, 0)
            };

            VBOScreen = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferScreen.Length), vertBufferScreen, BufferUsageHint.StaticDraw);

            VBOmouse = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOmouse);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferMouse.Length), vertBufferMouse, BufferUsageHint.StaticDraw);

            VBOdisconnected = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOdisconnected);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferDisconnected.Length), vertBufferDisconnected, BufferUsageHint.StaticDraw);
        }

        private void SetupViewport() {
            //OpenTkControl.MakeCurrent();

            screenWidth = glControl.Width;
            screenHeight = glControl.Height;

            targetAspectRatio = (float)virtualWidth / (float)virtualHeight;

            int width = screenWidth;
            int height = (int)((float)width / targetAspectRatio/* + 0.5f*/);

            if (height > screenHeight) {
                //Pillarbox
                //It doesn't fit our height, we must switch to pillarbox then
                height = screenHeight;
                width = (int)((float)height * targetAspectRatio/* + 0.5f*/);
            }

            // set up the new viewport centered in the backbuffer
            vpX = (screenWidth / 2) - (width / 2);
            vpY = (screenHeight / 2) - (height / 2);

            GL.Viewport(vpX, vpY, width, height);
            //GL.Viewport(0, 0, width, height);

            GL.MatrixMode(MatrixMode.Projection);
            //GL.PushMatrix();
            GL.LoadIdentity();
            GL.Ortho(0, virtualWidth, virtualHeight, 0, -1, 1); // Should be 2D

            GL.MatrixMode(MatrixMode.Modelview);
            //GL.PushMatrix();

            //Now to calculate the scale considering the screen size and virtual size
            scaleX = (double)screenWidth / (double)virtualWidth;
            scaleY = (double)screenHeight / (double)virtualHeight;
            GL.Scale(scaleX, scaleY, 1.0f);

            GL.LoadIdentity();
            // From now on, instead of using -1 < 0 < 1 co-ordinates, use pixel ones starting from 0,0 top left

            GL.Disable(EnableCap.DepthTest);

            //BuildOverlay2dMouse(true);
            //BuildOverlay2dKeyboard(true);
        }

        public void NotifySocketClosed(string sessionId) {
            if (sessionId == "/control/agent") {
            } else if(this.sessionId != sessionId)
                return;

            socketAlive = false;
            connectionStatus = ConnectionStatus.Disconnected;

            rc = null;
            Dispatcher.Invoke((Action)delegate {
                toolLatency.Header = "N/C";
            });

            glControl.Invalidate();
        }

        private void Render() {
            glControl.MakeCurrent();

            if (virtualRequireViewportUpdate) {
                RefreshVirtual();
                virtualRequireViewportUpdate = false;
            }
            SetupViewport(); //Different to KLCAlt

            //--

            if (textureNew) {
                GL.BindTexture(TextureTarget.Texture2D, textureBottom);

                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0, //Level
                    PixelInternalFormat.Rgb,
                    textureWidth,
                    textureHeight,
                    0, //Border
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
                    PixelType.UnsignedByte,
                    textureData); //bmpData.Scan0

                textureNew = false;
            }

            if (Settings.DisplayOverlayMouse && overlayNewMouse) {
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dMouse);

                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0, //Level
                    PixelInternalFormat.Rgba,
                    overlay2dMouse.Width,
                    overlay2dMouse.Height,
                    0, //Border
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    textureOverlayDataMouse);

                overlayNewMouse = false;
            }

            if ((Settings.DisplayOverlayKeyboardOther || Settings.DisplayOverlayKeyboardMod) && overlayNewKeyboard) {
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dKeyboard);

                GL.TexImage2D(
                    TextureTarget.Texture2D,
                    0, //Level
                    PixelInternalFormat.Rgba,
                    overlay2dKeyboard.Width,
                    overlay2dKeyboard.Height,
                    0, //Border
                    OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                    PixelType.UnsignedByte,
                    textureOverlayDataKeyboard);

                overlayNewKeyboard = false;
            }

            switch(connectionStatus) {
                case ConnectionStatus.FirstConnectionAttempt:
                    GL.ClearColor(System.Drawing.Color.SlateGray);
                    break;
                case ConnectionStatus.Connected:
                    if(controlEnabled)
                        GL.ClearColor(System.Drawing.Color.Black);
                    else
                        GL.ClearColor(System.Drawing.Color.MidnightBlue);
                    break;
                case ConnectionStatus.Disconnected:
                    GL.ClearColor(System.Drawing.Color.Maroon);
                    break;
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindTexture(TextureTarget.Texture2D, textureBottom);
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);
            GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
            GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
            GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferScreen.Length / 2);

            //--

            if (Settings.DisplayOverlayMouse) {
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dMouse);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOmouse);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferMouse.Length / 2);
            }

            if (Settings.DisplayOverlayKeyboardOther || Settings.DisplayOverlayKeyboardMod) {
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dKeyboard);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOkeyboard);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferKeyboard.Length / 2);
            }

            if (connectionStatus == ConnectionStatus.FirstConnectionAttempt || connectionStatus == ConnectionStatus.Disconnected) {
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dDisconnected);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOdisconnected);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferDisconnected.Length / 2);
            }

            //--

            glControl.SwapBuffers();
        }

        private void InitOverlayTexture(ref Bitmap overlay2d, ref int textureOverlay2d, int overlayW = overlayWidth, int overlayH = overlayHeight) {
            overlay2d = new Bitmap(overlayW, overlayH);
            textureOverlay2d = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, textureOverlay2d);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, overlay2d.Width, overlay2d.Height, 0,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero); // just allocate memory, so we can update efficiently using TexSubImage2D
        }

        private void InitScreenTexture() {
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);

            textureBottom = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, textureBottom);
            GL.TexImage2D(TextureTarget.Texture2D, 0,
                PixelInternalFormat.Rgb,
                virtualWidth, virtualHeight, 0, //W, H, Border
                OpenTK.Graphics.OpenGL.PixelFormat.Bgr,
                PixelType.UnsignedByte, IntPtr.Zero);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        }

        private void InitTextures() {
            //FPS/Mouse/Keyboard
            InitOverlayTexture(ref overlay2dMouse, ref textureOverlay2dMouse);
            InitOverlayTexture(ref overlay2dKeyboard, ref textureOverlay2dKeyboard);
            InitOverlayTexture(ref overlay2dDisconnected, ref textureOverlay2dDisconnected, 400);

            using (Graphics gfx = Graphics.FromImage(overlay2dDisconnected)) {
                //gfx.Clear(System.Drawing.Color.Transparent);
                gfx.Clear(System.Drawing.Color.FromArgb(128, 0, 0, 0));

                using (GraphicsPath gp = new GraphicsPath())
                using (System.Drawing.Pen outline = new System.Drawing.Pen(System.Drawing.Color.Black, 4) { LineJoin = LineJoin.Round }) //outline width=1
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (System.Drawing.Brush foreBrush = new SolidBrush(System.Drawing.Color.Lime)) {
                    gp.AddString("Disconnected", arial.FontFamily, (int)arial.Style, arial.Size, gfx.VisibleClipBounds, sf);
                    gfx.DrawPath(outline, gp);
                    gfx.FillPath(foreBrush, gp);
                }

                BitmapData data2 = overlay2dDisconnected.LockBits(new System.Drawing.Rectangle(0, 0, overlay2dDisconnected.Width, overlay2dDisconnected.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                if (textureOverlayDataDisconnected == null)
                    textureOverlayDataDisconnected = new byte[Math.Abs(data2.Stride * data2.Height)];
                Marshal.Copy(data2.Scan0, textureOverlayDataDisconnected, 0, textureOverlayDataDisconnected.Length);

                overlay2dDisconnected.UnlockBits(data2);
            }

            GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dDisconnected);

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0, //Level
                PixelInternalFormat.Rgba,
                overlay2dDisconnected.Width,
                overlay2dDisconnected.Height,
                0, //Border
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                PixelType.UnsignedByte,
                textureOverlayDataDisconnected);

            InitScreenTexture();

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.Enable(EnableCap.Texture2D);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha); //NTR org

            //--

            //Warning! Mouse debug position is changed to the right when the virtual screen size is changed!
            vertBufferMouse = new Vector2[8] {
                new Vector2(0,overlayHeight), new Vector2(0, 1),
                new Vector2(overlayWidth,overlayHeight), new Vector2(1, 1),
                new Vector2(overlayWidth,0), new Vector2(1, 0),
                new Vector2(0,0), new Vector2(0, 0)
            };

            vertBufferKeyboard = new Vector2[8] {
                new Vector2(0,overlayHeight), new Vector2(0, 1),
                new Vector2(overlayWidth,overlayHeight), new Vector2(1, 1),
                new Vector2(overlayWidth,0), new Vector2(1, 0),
                new Vector2(0,0), new Vector2(0, 0)
            };

            //Warning! Disconnected overlay position is changed to the right when the virtual screen size is changed!
            vertBufferDisconnected = new Vector2[8] {
                new Vector2(0,overlayHeight), new Vector2(0, 1),
                new Vector2(400,overlayHeight), new Vector2(1, 1),
                new Vector2(400,0), new Vector2(1, 0),
                new Vector2(0,0), new Vector2(0, 0)
            };

            VBOmouse = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOmouse);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferMouse.Length), vertBufferMouse, BufferUsageHint.StaticDraw);

            VBOkeyboard = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOkeyboard);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferKeyboard.Length), vertBufferKeyboard, BufferUsageHint.StaticDraw);

            VBOdisconnected = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOdisconnected);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferDisconnected.Length), vertBufferDisconnected, BufferUsageHint.StaticDraw);
        }

        public void UpdateLatency(long ms) {
            Dispatcher.Invoke((Action)delegate {
                toolLatency.Header = string.Format("{0} ms", ms);
            });
        }

        public void ClearScreens() {
            listScreen.Clear();
            currentScreen = null;

            Dispatcher.Invoke((Action)delegate {
                toolScreen.Items.Clear();
            });
        }

        public void AddScreen(string screen_id, string screen_name, int screen_height, int screen_width, int screen_x, int screen_y) {
            RCScreen newScreen = new RCScreen(screen_id, screen_name, screen_height, screen_width, screen_x, screen_y);
            listScreen.Add(newScreen);
            if (currentScreen == null) {
                currentScreen = newScreen;

                virtualHeight = currentScreen.rect.Height;
                virtualWidth = currentScreen.rect.Width;
                virtualRequireViewportUpdate = true;
            }

            Dispatcher.Invoke((Action)delegate {
                MenuItem item = new MenuItem();
                item.Header = screen_name + ":" + screen_x + "," + screen_y;
                item.Click += new RoutedEventHandler(toolScreen_ItemClicked);

                toolScreen.Items.Add(item);

                //Private and Mac seem to bug out if you change screens, cause there's only one screen
                toolScreen.IsEnabled = (listScreen.Count > 1);
            });
        }

        private void toolScreen_ItemClicked(object sender, RoutedEventArgs e) {
            if (rc == null)
                return;

            MenuItem source = (MenuItem)e.Source;
            string[] screen_selected = source.Header.ToString().Split(':');

            currentScreen = listScreen.First(x => x.screen_name == screen_selected[0]);
            rc.ChangeScreen(currentScreen.screen_id);
        }

        public void LoadTexture(int width, int height, Bitmap decomp, string screenId) {
            if (currentScreen == null)
                return;

            if (virtualWidth != width || virtualHeight != height) {
                Console.WriteLine("Virtual resolution did not match texture received.");
                SetVirtual(width, height);

                try {
                    currentScreen.rect.Width = width;
                    currentScreen.rect.Height = height;
                    //This is a sad attempt a fixing a problem when changing left monitor's size.
                    //However if changing a middle monitor, the right monitor will break.
                    //The reconnect button can otherwise be used, or perhaps a multimonitor/scan feature can be added to automatically detect and repair the list of screens.
                    if (currentScreen.rect.X < 0)
                        currentScreen.rect.X = width * -1;
                } catch(Exception ex) {
                    Console.WriteLine("[LoadTexture] " + ex.ToString());
                }
                
                //textureData = null; //This seems to make it crack
                //Need to find a better way to load the texture in, maybe just before render?
                //return;
            }

            //qt.v = textureBottom;
            //qtBottom = qt;

            textureWidth = width;
            textureHeight = height;

            BitmapData data = decomp.LockBits(new System.Drawing.Rectangle(0, 0, decomp.Width, decomp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            if (textureData == null || virtualRequireViewportUpdate)
                textureData = new byte[Math.Abs(data.Stride * data.Height)];
            Marshal.Copy(data.Scan0, textureData, 0, textureData.Length); //This can fail with re-taking over private remote control
            decomp.UnlockBits(data);

            textureNew = true;
            socketAlive = true;

            glControl.Invalidate();
        }

        #region Handle
        /*
        private void HandleKeyPress(object sender, System.Windows.Forms.KeyPressEventArgs e) {
            if (!controlEnabled || rc == null)
                return;

            Console.WriteLine("This shouldn't be used");
            //rc.SendKeyDown((int)e.KeyChar);
            //rc.SendKeyUp((int)e.KeyChar);
            //Console.WriteLine(e.KeyChar + " - " + (byte)e.KeyChar);
        }
        */

        private void toolSendCtrlAltDel_Click(object sender, RoutedEventArgs e) {
            if (!controlEnabled || rc == null)
                return;

            rc.SendSecureAttentionSequence();
        }

        private void SyncClipboard(object sender, EventArgs e) {
            try {
                if (Settings.ClipboardSyncEnabled) {
                    this.toolClipboardSend_Click(sender, e);
                    Console.WriteLine("[Clipboard sync] Success?");
                }
            } catch (Exception) {
                Console.WriteLine("[Clipboard sync] Fail");
            }
        }

        private void toolClipboardSync_Click(object sender, RoutedEventArgs e) {
            Settings.ClipboardSyncEnabled = !Settings.ClipboardSyncEnabled;
            //toolClipboardSync.Overflow = (clipboardSyncEnabled ? ToolStripItemOverflow.AsNeeded : ToolStripItemOverflow.Always);

            if (Settings.ClipboardSyncEnabled)
                toolClipboardSync.Header = "Clipboard (Synced)";
            else
                toolClipboardSync.Header = "Clipboard (Receive Only)";
        }

        private void toolClipboardSend_Click(object sender, EventArgs e) {
            if (rc == null)
                return;

            clipboard = Clipboard.GetText();
            Dispatcher.Invoke((Action)delegate {
                toolClipboardSend.Header = "Send to Client: " + clipboard.Replace("\r", "").Replace("\n", "").Truncate(5);
                rc.SendClipboard(clipboard);

                if (Settings.ClipboardSyncEnabled)
                    toolClipboardGet.Header = "Get from Client: " + clipboard.Replace("\r", "").Replace("\n", "").Truncate(5);
            });
        }

        public void ReceiveClipboard(string content) {
            clipboard = content;
            Dispatcher.Invoke((Action)delegate {
                //try {
                    toolClipboardGet.Header = "Get from Client: " + clipboard.Replace("\r", "").Replace("\n", "").Truncate(5);

                    //if (clipboardSyncEnabled) { //Commented out now that we use Receive-Only mode
                    //this.BeginInvoke(new Action(() => {
                    if (clipboard.Length > 0)
                        Clipboard.SetDataObject(clipboard);
                        //Clipboard.SetText(clipboard); //Apparently this doesn't work
                    else
                        Clipboard.Clear();
                    //}));
                    //}
                //} catch(Exception ex) {
                    //new WindowException(ex, ex.GetType().ToString()).Show();
                //}
            });
        }

        private void toolClipboardGet_Click(object sender, RoutedEventArgs e) {
            if (clipboard.Length > 0)
                Clipboard.SetText(clipboard);
        }

        private void toolToggleControl_Click(object sender, RoutedEventArgs e) {
            SetControlEnabled(!controlEnabled);
        }

        public void SetControlEnabled(bool value, bool isStart=false) {
            if (isStart && !Settings.StartControlEnabled)
                return;

            controlEnabled = value;

            Dispatcher.Invoke((Action)delegate {
                if (controlEnabled)
                    toolToggleControl.Header = "Control Enabled";
                else
                    toolToggleControl.Header = "Control Disabled";

                glControl.Invalidate();
            });
        }

        private List<KeycodeV2> listHeldKeysMod = new List<KeycodeV2>();
        private List<KeycodeV2> listHeldKeysOther = new List<KeycodeV2>();

        private void toolKeyWin_Click(object sender, RoutedEventArgs e) {
            KeyWinSet(!keyDownWin);
        }

        private void toolDebugKeyboardMod_Click(object sender, RoutedEventArgs e) {
            toolDebugKeyboardMod.IsChecked = Settings.DisplayOverlayKeyboardMod = !Settings.DisplayOverlayKeyboardMod;
        }

        private void toolDebugKeyboardOther_Click(object sender, RoutedEventArgs e) {
            toolDebugKeyboardOther.IsChecked = Settings.DisplayOverlayKeyboardOther = !Settings.DisplayOverlayKeyboardOther;
        }

        private void toolDebugMouse_Click(object sender, RoutedEventArgs e) {
            toolDebugMouse.IsChecked = Settings.DisplayOverlayMouse = !Settings.DisplayOverlayMouse;
        }

        private void toolReconnect_Click(object sender, RoutedEventArgs e) {
            if(rc != null)
                rc.Reconnect();
        }

        KeycodeV2 keyshift = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.ShiftKey);
        KeycodeV2 keywin = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.LWin);

        private void HandleMouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!controlEnabled || rc == null)
                return;

            if(e.Button == System.Windows.Forms.MouseButtons.Middle) {
                string text = Clipboard.GetText().Trim();

                if (!text.Contains('\n') && !text.Contains('\r')) {
                    //Console.WriteLine("Attempt autotype of " + text, "autotype");

                    bool confirmed;
                    if (text.Length < 51 || autotypeAlwaysConfirmed) {
                        confirmed = true;
                    } else {
                        WindowConfirmation winConfirm = new WindowConfirmation("You really want to autotype this?", text);
                        confirmed = (bool)winConfirm.ShowDialog();
                        if (confirmed && (bool)winConfirm.chkDoNotAsk.IsChecked)
                            autotypeAlwaysConfirmed = true;
                    }
                    if(confirmed)
                        rc.SendAutotype(text);

                } else {
                    Console.WriteLine("Autotype blocked: too long or had a new line character");
                }
            } else {
                if(windowActivatedMouseMove)
                    HandleMouseMove(sender, e);

                rc.SendMouseDown(e.Button);

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    mouseHeldLeft = true;
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    mouseHeldRight = true;

                DebugKeyboard();
            }

        }

        private void toolPanicRelease_Click(object sender, RoutedEventArgs e) {
            if (!controlEnabled || rc == null)
                return;

            rc.SendPanicKeyRelease();
            listHeldKeysMod.Clear();
            KeyWinSet(false);

            DebugKeyboard();
        }

        private void toolScreenshotToClipboard_Click(object sender, RoutedEventArgs e) {
            if (rc == null)
                return;

            rc.CaptureNextScreen();
        }

        private void glControl_Resize(object sender, EventArgs e) {
            virtualRequireViewportUpdate = true;
        }

        private void glControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e) {
            //This happens when initialized and when resized
            Render();
        }

        private void glControl_Load(object sender, EventArgs e) {
            InitTextures();
            RefreshVirtual();

            //glControl.PreviewKeyDown += HandlePreviewKeyDown;
            //glControl.KeyUp += HandleKeyUp;
            glControl.MouseMove += HandleMouseMove;
            glControl.MouseDown += HandleMouseDown;
            glControl.MouseUp += HandleMouseUp;
            glControl.MouseWheel += HandleMouseWheel;
        }

        private void toolShowAlternative_Click(object sender, RoutedEventArgs e) {
            if (App.alternative == null)
                return;

            App.alternative.Visibility = Visibility.Visible;
            App.alternative.Focus();

            foreach (System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens) {
                if(screen.Bounds.IntersectsWith(new System.Drawing.Rectangle((int)this.Left, (int)this.Top, (int)this.Width, (int)this.Height))) {
                    App.alternative.Left = screen.Bounds.X + ((screen.Bounds.Width - App.alternative.Width) / 2);
                    App.alternative.Top = screen.Bounds.Y + ((screen.Bounds.Height - App.alternative.Height) / 2);
                    break;
                }
            }
        }

        private void Window_Activated(object sender, EventArgs e) {
            if (!controlEnabled || currentScreen == null || rc == null)
                return;

            windowActivatedMouseMove = true;
        }

        private void Window_Deactivated(object sender, EventArgs e) {
            if (!controlEnabled || currentScreen == null || rc == null)
                return;

            //Release modifier keys because the remote control window lost focus
            if (listHeldKeysMod.Count > 0) {
                foreach (KeycodeV2 k in listHeldKeysMod)
                    rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                listHeldKeysMod.Clear();

                KeyWinSet(false);

                DebugKeyboard();
            }
        }

        private void toolSaveSettings_Click(object sender, RoutedEventArgs e) {
            Settings.Save();
        }

        private void Window_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                //Console.WriteLine(files[0]);
                rc.UploadDrop(files[0]);
            }
        }

        private void toolSettingStartControlEnabled_Click(object sender, RoutedEventArgs e) {
            toolSettingStartControlEnabled.IsChecked = Settings.StartControlEnabled = !Settings.StartControlEnabled;
        }

        private void HandleMouseUp(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!controlEnabled || rc == null)
                return;

            if (glControl.ClientRectangle.Contains(e.Location)) {
                rc.SendMouseUp(e.Button);

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    mouseHeldLeft = false;
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    mouseHeldRight = false;

                DebugKeyboard();
            }
        }

        private void HandleMouseMove(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!controlEnabled || currentScreen == null || rc == null || !this.IsActive)
                return;

            windowActivatedMouseMove = false;

            System.Drawing.Point point = new System.Drawing.Point(e.X - vpX, e.Y - vpY);
            if (point.X < 0 || point.Y < 0)
                if (point.X < 0 || point.Y < 0)
                return;

            if (vpX > 0) {
                point.X = (int)(point.X / scaleY);
                point.Y = (int)(point.Y / scaleY);
            } else {
                point.X = (int)(point.X / scaleX);
                point.Y = (int)(point.Y / scaleX);
            }

            if (point.X > virtualWidth || point.Y > virtualHeight)
                return;

            point.X = point.X + currentScreen.rect.X;
            point.Y = point.Y + currentScreen.rect.Y;

            DebugMouseEvent(point.X, point.Y);

            rc.SendMousePosition(point.X, point.Y);
        }

        private void HandleMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!controlEnabled || rc == null)
                return;

            rc.SendMouseWheel(e.Delta);
        }

        private void OpenTkControl_PreviewKeyDown(object sender, KeyEventArgs e) {
            //Apparently preview is used because arrow keys?
            if (!controlEnabled || rc == null)
                return;

            System.Windows.Forms.KeyEventArgs e2 = e.ToWinforms();

            if (e2.KeyCode == System.Windows.Forms.Keys.Pause) {
                //Done on release
                e.Handled = true;
                return;
            } else {

                KeycodeV2 keykaseyaUN = KeycodeV2.ListUnhandled.Find(x => x.Key == e2.KeyCode);
                if (keykaseyaUN != null)
                    return;

                try {
                    KeycodeV2 keykaseya = KeycodeV2.List.Find(x => x.Key == e2.KeyCode);

                    if (keykaseya == null)
                        throw new KeyNotFoundException(e2.KeyCode.ToString());

                    if (keykaseya.Key == System.Windows.Forms.Keys.LWin || keykaseya.Key == System.Windows.Forms.Keys.RWin) {
                        KeyWinSet(true);
                    }

                    if(keykaseya.IsMod) {
                        if (!listHeldKeysMod.Contains(keykaseya))
                            listHeldKeysMod.Add(keykaseya);
                    } else {
                        if (!listHeldKeysOther.Contains(keykaseya))
                            listHeldKeysOther.Add(keykaseya);
                    }

                    //Still allow holding it down
                    rc.SendKeyDown(keykaseya.JavascriptKeyCode, keykaseya.USBKeyCode);
                } catch {
                    Console.WriteLine("DOWN scan: " + e2.KeyCode + " / " + e2.KeyData + " / " + e2.KeyValue);
                }

                DebugKeyboard();
            }

            e.Handled = true;
        }

        private void OpenTkControl_KeyUp(object sender, KeyEventArgs e) {
            if (rc == null)
                return;

            System.Windows.Forms.KeyEventArgs e2 = e.ToWinforms();

            if (e2.KeyCode == System.Windows.Forms.Keys.PrintScreen) {
                rc.CaptureNextScreen();
            } else if (!controlEnabled || rc == null) {
                //Do nothing
            } else if (e2.KeyCode == System.Windows.Forms.Keys.Pause) {
                rc.SendPanicKeyRelease();
                listHeldKeysMod.Clear();
                KeyWinSet(false);
            } else {
                KeycodeV2 keykaseyaUN = KeycodeV2.ListUnhandled.Find(x => x.Key == e2.KeyCode);
                if (keykaseyaUN != null)
                    return;

                try {
                    KeycodeV2 keykaseya = KeycodeV2.List.Find(x => x.Key == e2.KeyCode);

                    if (keykaseya == null)
                        throw new KeyNotFoundException(e2.KeyCode.ToString());

                    bool removed = (keykaseya.IsMod ? listHeldKeysMod.Remove(keykaseya) : listHeldKeysOther.Remove(keykaseya));

                    rc.SendKeyUp(keykaseya.JavascriptKeyCode, keykaseya.USBKeyCode);

                    if (keyDownWin) {
                        foreach (KeycodeV2 k in listHeldKeysOther)
                            rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                        listHeldKeysOther.Clear();
                        foreach (KeycodeV2 k in listHeldKeysMod)
                            rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                        listHeldKeysMod.Clear();

                        KeyWinSet(false);
                    }
                } catch {
                    Console.WriteLine("Up scan: " + e2.KeyCode + " / " + e2.KeyData + " / " + e2.KeyValue);
                }

                DebugKeyboard();
            }

            e.Handled = true;
        }

        /*
        private void HandlePreviewKeyDown(object sender, System.Windows.Forms.PreviewKeyDownEventArgs e) {
            HandleKeyDown(sender, new System.Windows.Forms.KeyEventArgs(e.KeyData));
        }
        */

        private bool mouseHeldLeft = false;
        private bool mouseHeldRight = false;

        private void KeyWinSet(bool set) {
            if (!controlEnabled || rc == null)
                return;

            keyDownWin = set;

            if (keyDownWin) {
                if (!listHeldKeysMod.Contains(keywin)) {
                    listHeldKeysMod.Add(keywin);
                    rc.SendKeyDown(keywin.JavascriptKeyCode, keywin.USBKeyCode);
                }
            } else {
                if (listHeldKeysMod.Contains(keywin)) {
                    listHeldKeysMod.Remove(keywin);
                    rc.SendKeyUp(keywin.JavascriptKeyCode, keywin.USBKeyCode);
                }
            }

            toolKeyWin.FontWeight = (keyDownWin ? FontWeights.Bold : FontWeights.Normal);
        }

        private void DebugMouseEvent(int X, int Y) {
            //Console.WriteLine("vp(" + vpX + "," + vpY + ") - aspect:" + targetAspectRatio + " - scale(" + scaleX + ", " + scaleY + ") black(" + pointBlack.ToString() + ") // " + point.ToString() + " - (" + X + ", " + Y + ")");

            string strMousePos = string.Format("X: {0} - Y: {1}", X, Y);

            using (Graphics gfx = Graphics.FromImage(overlay2dMouse)) {
                gfx.Clear(System.Drawing.Color.Transparent);

                using (GraphicsPath gp = new GraphicsPath())
                using (System.Drawing.Pen outline = new System.Drawing.Pen(System.Drawing.Color.Black, 4) { LineJoin = LineJoin.Round }) //outline width=1
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Far })
                using (System.Drawing.Brush foreBrush = new SolidBrush(System.Drawing.Color.Lime)) {
                    gp.AddString(strMousePos, arial.FontFamily, (int)arial.Style, arial.Size, gfx.VisibleClipBounds, sf);
                    gfx.DrawPath(outline, gp);
                    gfx.FillPath(foreBrush, gp);
                }

                BitmapData data2 = overlay2dMouse.LockBits(new System.Drawing.Rectangle(0, 0, overlay2dMouse.Width, overlay2dMouse.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                if (textureOverlayDataMouse == null)
                    textureOverlayDataMouse = new byte[Math.Abs(data2.Stride * data2.Height)];
                Marshal.Copy(data2.Scan0, textureOverlayDataMouse, 0, textureOverlayDataMouse.Length);

                //qtOverlay2d.Load(overlay2d.Width, overlay2d.Height, bData);
                overlay2dMouse.UnlockBits(data2);

                overlayNewMouse = true;
            }
        }

        private void DebugKeyboard() {
            string strKeyboard = "";

            if (Settings.DisplayOverlayKeyboardMod) {
                string[] keysMod = new string[listHeldKeysMod.Count];
                for (int i = 0; i < keysMod.Length; i++) {
                    keysMod[i] = listHeldKeysMod[i].Display;
                }

                strKeyboard += String.Join(", ", keysMod);
            }

            if (Settings.DisplayOverlayKeyboardOther) {
                string[] keysOther = new string[listHeldKeysOther.Count];
                for (int i = 0; i < keysOther.Length; i++) {
                    keysOther[i] = listHeldKeysOther[i].Display;
                }

                if (strKeyboard.Length > 0 && keysOther.Length > 0)
                    strKeyboard += " | ";
                strKeyboard += String.Join(", ", keysOther);

                if (mouseHeldRight)
                    strKeyboard = "MouseRight" + (strKeyboard == "" ? "" : " | " + strKeyboard);
                if (mouseHeldLeft)
                    strKeyboard = "MouseLeft" + (strKeyboard == "" ? "" : " | " + strKeyboard);
            }

            using (Graphics gfx = Graphics.FromImage(overlay2dKeyboard)) {
                gfx.Clear(System.Drawing.Color.Transparent);

                using (GraphicsPath gp = new GraphicsPath())
                using (System.Drawing.Pen outline = new System.Drawing.Pen(System.Drawing.Color.Black, 4) { LineJoin = LineJoin.Round }) //outline width=1
                using (StringFormat sf = new StringFormat())
                using (System.Drawing.Brush foreBrush = new SolidBrush(System.Drawing.Color.Lime)) {
                    gp.AddString(strKeyboard, arial.FontFamily, (int)arial.Style, arial.Size, new PointF(0, 0), sf);
                    gfx.DrawPath(outline, gp);
                    gfx.FillPath(foreBrush, gp);
                }

                BitmapData data2 = overlay2dKeyboard.LockBits(new System.Drawing.Rectangle(0, 0, overlay2dKeyboard.Width, overlay2dKeyboard.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                if (textureOverlayDataKeyboard == null)
                    textureOverlayDataKeyboard = new byte[Math.Abs(data2.Stride * data2.Height)];
                Marshal.Copy(data2.Scan0, textureOverlayDataKeyboard, 0, textureOverlayDataKeyboard.Length);

                overlay2dKeyboard.UnlockBits(data2);

                overlayNewKeyboard = true;
            }
        }
        #endregion
    }
}
