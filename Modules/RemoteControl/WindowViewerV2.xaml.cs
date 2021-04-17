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
    public partial class WindowViewerV2 : Window {

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

        private Rectangle virtualCanvas, virtualViewWant, virtualViewNeed;
        int vpX, vpY;
        double scaleX, scaleY;

        private Camera MainCamera;

        //private string sessionId;

        private static Font arial = new Font("Arial", 32);
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
        private const int overlayWidth = 400;
        private const int overlayHeight = 100;

        Vector2[] vertBufferScreen;
        Vector2[] vertBufferMouse, vertBufferKeyboard, vertBufferDisconnected;
        int VBOScreen;
        int VBOmouse, VBOkeyboard, VBOdisconnected;

        private bool keyDownWin;
        private bool autotypeAlwaysConfirmed;
        private bool windowActivatedMouseMove;

        enum ConnectionStatus {
            FirstConnectionAttempt,
            Connected,
            Disconnected
        }
        ConnectionStatus connectionStatus;

        //--

        public WindowViewerV2(RemoteControl rc, int virtualWidth = 1920, int virtualHeight = 1080) {
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

            MainCamera = new Camera(Vector2.Zero);

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

        public void SetCanvas(int virtualX, int virtualY, int virtualWidth, int virtualHeight)
        {
            virtualCanvas = new Rectangle(virtualX, virtualY, Math.Abs(virtualX) + virtualWidth, Math.Abs(virtualY) + virtualHeight);
            SetVirtual(virtualX, virtualY, virtualCanvas.Width, virtualCanvas.Height);
        }

        public void SetVirtual(int virtualX, int virtualY, int virtualWidth, int virtualHeight)
        {
            virtualViewWant = new Rectangle(virtualX, virtualY, virtualWidth, virtualHeight);
            virtualRequireViewportUpdate = true;
        }

        private void RefreshVirtual() {
            vertBufferScreen = new Vector2[8] {
                new Vector2(virtualCanvas.Left, virtualCanvas.Top), new Vector2(0, 1),
                new Vector2(virtualCanvas.Right, virtualCanvas.Top), new Vector2(1, 1),
                new Vector2(virtualCanvas.Right, virtualCanvas.Bottom), new Vector2(1, 0),
                new Vector2(virtualCanvas.Left, virtualCanvas.Bottom), new Vector2(0, 0)
            };

            VBOScreen = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferScreen.Length), vertBufferScreen, BufferUsageHint.StaticDraw);

            vertBufferMouse = new Vector2[8] {
                new Vector2(glControl.Width - overlayWidth,overlayHeight), new Vector2(0, 1),
                new Vector2(glControl.Width,overlayHeight), new Vector2(1, 1),
                new Vector2(glControl.Width,0), new Vector2(1, 0),
                new Vector2(glControl.Width - overlayWidth,0), new Vector2(0, 0)
            };

            VBOmouse = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOmouse);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferMouse.Length), vertBufferMouse, BufferUsageHint.StaticDraw);

            vertBufferKeyboard = new Vector2[8] {
                new Vector2(0,overlayHeight), new Vector2(0, 1),
                new Vector2(overlayWidth,overlayHeight), new Vector2(1, 1),
                new Vector2(overlayWidth,0), new Vector2(1, 0),
                new Vector2(0,0), new Vector2(0, 0)
            };

            VBOkeyboard = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOkeyboard);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferKeyboard.Length), vertBufferKeyboard, BufferUsageHint.StaticDraw);

            int leftDisconnected = (glControl.Width - 400) / 2;
            int topDisconnected = (glControl.Height - overlayHeight) / 2;
            vertBufferDisconnected = new Vector2[8] {
                new Vector2(leftDisconnected, topDisconnected + overlayHeight), new Vector2(0, 1),
                new Vector2(leftDisconnected + 400, topDisconnected + overlayHeight), new Vector2(1, 1),
                new Vector2(leftDisconnected + 400, topDisconnected), new Vector2(1, 0),
                new Vector2(leftDisconnected, topDisconnected), new Vector2(0, 0)
            };

            VBOdisconnected = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOdisconnected);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferDisconnected.Length), vertBufferDisconnected, BufferUsageHint.StaticDraw);
        }

        public void NotifySocketClosed(string sessionId) {
            if (sessionId == "/control/agent") {
            } else if (this.sessionId != sessionId)
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
                float currentAspectRatio = (float)glControl.Width / (float)glControl.Height;
                float targetAspectRatio = (float)virtualViewWant.Width / (float)virtualViewWant.Height;
                int width = virtualViewWant.Width;
                int height = virtualViewWant.Height;
                vpX = 0;
                vpY = 0;

                if (currentAspectRatio > targetAspectRatio) {
                    //Pillarbox
                    width = (int)((float)height * currentAspectRatio);
                    vpX = (width - virtualViewWant.Width) / 2;
                } else {
                    //Letterbox
                    height = (int)((float)width / currentAspectRatio);
                    vpY = (height - virtualViewWant.Height) / 2;
                }

                scaleX = (double)glControl.Width / (double)width;
                scaleY = (double)glControl.Height / (double)height;

                virtualViewNeed = new Rectangle(virtualViewWant.X - vpX, virtualViewWant.Y - vpY, width, height);

                virtualRequireViewportUpdate = false;
            }

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(virtualViewNeed.Left, virtualViewNeed.Right, virtualViewNeed.Bottom, virtualViewNeed.Top, MainCamera.ZNear, MainCamera.ZFar);
            GL.Viewport(0, 0, glControl.Width, glControl.Height);
            MainCamera.ApplyTransform();
            GL.MatrixMode(MatrixMode.Modelview);
            //GL.LoadIdentity();
            //GL.Disable(EnableCap.DepthTest);

            //BuildOverlay2dMouse(true);
            //BuildOverlay2dKeyboard(true);

            //--

            foreach(RCScreen screen in listScreen) {
                if (screen.Texture == null) {
                    screen.Texture = new TextureScreen();
                } else
                    screen.Texture.RenderNew();
            }

            /*
            if (textureNew) {
                GL.BindTexture(TextureTarget.Texture2D, textureID);

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
            */

            #region Overlay
            if (overlayNewMouse) {
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

            if (overlayNewKeyboard) {
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
            #endregion

            switch (connectionStatus) {
                case ConnectionStatus.FirstConnectionAttempt:
                    GL.ClearColor(System.Drawing.Color.SlateGray);
                    break;
                case ConnectionStatus.Connected:
                    if (controlEnabled)
                        GL.ClearColor(System.Drawing.Color.Black);
                    else
                        GL.ClearColor(System.Drawing.Color.MidnightBlue);
                    break;
                case ConnectionStatus.Disconnected:
                    GL.ClearColor(System.Drawing.Color.Maroon);
                    break;
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            //GL.BindTexture(TextureTarget.Texture2D, textureScreenLegacy.ID);
            //GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);
            //GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
            //GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
            //GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferScreen.Length / 2);

            //GL.Disable(EnableCap.Texture2D);
            int screenNum = 0;

            

            foreach (RCScreen screen in listScreen)
            {
                if (false) {
                    GL.Disable(EnableCap.Texture2D);
                    //Test
                    if (screenNum == 0)
                        GL.Color3(Color.FromArgb(251, 218, 3)); //Yellow
                    else if (screenNum == 1)
                        GL.Color3(Color.FromArgb(255, 165, 50)); //Orrange
                    else if (screenNum == 2)
                        GL.Color3(Color.FromArgb(53, 166, 170)); //Teal
                    else if (screenNum == 3)
                        GL.Color3(Color.FromArgb(220, 108, 167)); //Pink
                    else if (screenNum == 4)
                        GL.Color3(Color.FromArgb(57, 54, 122)); //Purple
                    else
                        GL.Color3(Color.White);

                    //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                    GL.Begin(PrimitiveType.Polygon);
                    GL.PointSize(5f);
                    GL.LineWidth(5f);

                    GL.Vertex2(screen.rect.Left, screen.rect.Bottom);
                    GL.Vertex2(screen.rect.Left, screen.rect.Top);
                    GL.Vertex2(screen.rect.Right, screen.rect.Top);
                    GL.Vertex2(screen.rect.Right, screen.rect.Bottom);

                    //GL.Vertex2(vertBufferScreen[0].X, vertBufferScreen[0].Y);

                    GL.End();

                    screenNum++;
                }

                if (screen.Texture != null) {
                    GL.Enable(EnableCap.Texture2D);
                    screen.Texture.Render();
                }
            }

            //--

            GL.Enable(EnableCap.Texture2D);

            #region Overlay

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, glControl.Width, glControl.Height, 0, MainCamera.ZNear, MainCamera.ZFar); //Test

            if (true) {
                //if (Settings.DisplayOverlayMouse) {
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dMouse);
                GL.Color3(Color.White);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOmouse);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferMouse.Length / 2);
                //}

                //if (Settings.DisplayOverlayKeyboardOther || Settings.DisplayOverlayKeyboardMod) {
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dKeyboard);
                //GL.Color3(Color.Green);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOkeyboard);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferKeyboard.Length / 2);
                //}

                if (connectionStatus == ConnectionStatus.FirstConnectionAttempt || connectionStatus == ConnectionStatus.Disconnected) {
                    GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dDisconnected);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, VBOdisconnected);
                    GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                    GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                    GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferDisconnected.Length / 2);
                }
            }
            #endregion

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

        private void InitScreenTexture(ref int textureID) {
            GL.PixelStore(PixelStoreParameter.UnpackAlignment, 1);
            textureID = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2D, textureID);
            GL.TexImage2D(TextureTarget.Texture2D, 0,
                PixelInternalFormat.Rgb,
                virtualCanvas.Width, virtualCanvas.Height, 0, //W, H, Border (!! this is probably wrong since the virtualCanvas was added)
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

            //--

            /*
            textureScreens = new TextureScreen[4];
            for (int i = 0; i < textureScreens.Length; i++) {
                textureScreens[i] = new TextureScreen();
                InitScreenTexture(ref textureScreens[i].ID);
            }
            textureScreenLegacy = textureScreens[0];
            */

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.Enable(EnableCap.Texture2D);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha); //NTR org
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

                //SetVirtual(screen_x, screen_y, screen_width, screen_height);
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
            MenuItem source = (MenuItem)e.Source;
            string[] screen_selected = source.Header.ToString().Split(':');

            currentScreen = listScreen.First(x => x.screen_name == screen_selected[0]);
            rc.ChangeScreen(currentScreen.screen_id);

            //MainCamera.Rotation = 0f;
            MainCamera.Position = Vector2.Zero;
            MainCamera.Scale = new Vector2(1f, 1f);
            DebugKeyboard();

            SetVirtual(currentScreen.rect.X, currentScreen.rect.Y, currentScreen.rect.Width, currentScreen.rect.Height);
            glControl.Invalidate();
        }

        private void toolScreenOverview_Click(object sender, RoutedEventArgs e)
        {
            //MainCamera.Rotation = 0f;
            MainCamera.Position = Vector2.Zero;
            MainCamera.Scale = new Vector2(1f, 1f);
            DebugKeyboard();

            virtualViewWant = virtualCanvas;
            virtualRequireViewportUpdate = true;
            glControl.Invalidate();
        }

        /*
        public void LoadTexture(int width, int height, Bitmap decomp) {
            if (currentScreen.rect.Width != width || currentScreen.rect.Height != height) {
                Console.WriteLine("Current screen resolution did not match texture received.");

                try {
                    currentScreen.rect.Width = width;
                    currentScreen.rect.Height = height;

                    //This is a sad attempt a fixing a problem when changing left monitor's size.
                    //However if changing a middle monitor, the right monitor will break.
                    //The reconnect button can otherwise be used, or perhaps a multimonitor/scan feature can be added to automatically detect and repair the list of screens.
                    if (currentScreen.rect.X < 0)
                        currentScreen.rect.X = width * -1;

                    SetVirtual(currentScreen.rect.X, currentScreen.rect.Y, currentScreen.rect.Width, currentScreen.rect.Height);
                } catch(Exception ex) {
                    Console.WriteLine("[LoadTexture] " + ex.ToString());
                }
                
                //textureData = null; //This seems to make it crack
                //Need to find a better way to load the texture in, maybe just before render?
                //return;
            }

            textureScreenLegacy.Load(width, height, decomp, virtualRequireViewportUpdate);
            socketAlive = true;

            glControl.Invalidate();
        }
        */

        public void LoadTexture(int width, int height, Bitmap decomp, string screenID) {
            RCScreen scr = listScreen.FirstOrDefault(x => x.screen_id == screenID);
            if(scr == null) {
                Console.WriteLine("[LoadTexture] No matching RCScreen for screen ID: " + screenID);
                throw new NotImplementedException();
                return;
            }

            if(scr.rect.Width != width || scr.rect.Height != height){
                Console.WriteLine("Virtual resolution did not match texture received.");
                return;
                //SetVirtual(width, height);

                try {
                    scr.rect.Width = width;
                    scr.rect.Height = height;
                    if (scr.rect.X < 0)
                        scr.rect.X = width * -1;
                } catch (Exception ex) {
                    Console.WriteLine("[LoadTexture] " + ex.ToString());
                }
            }

            if(scr.Texture != null)
                scr.Texture.Load(scr.rect, decomp, virtualRequireViewportUpdate);
            socketAlive = true;

            glControl.Invalidate();
        }

        #region Handle
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
            if (clipboard == content)
                return;

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

        public void SetControlEnabled(bool value, bool isStart = false) {
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
            if (rc != null)
                rc.Reconnect();
        }

        KeycodeV2 keyshift = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.ShiftKey);
        KeycodeV2 keywin = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.LWin);

        private void HandleMouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!controlEnabled || rc == null)
                return;

            if (e.Button == System.Windows.Forms.MouseButtons.Middle) {
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
                    if (confirmed)
                        rc.SendAutotype(text);

                } else {
                    Console.WriteLine("Autotype blocked: too long or had a new line character");
                }
            } else {
                if (windowActivatedMouseMove)
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
            RefreshVirtual();
            virtualRequireViewportUpdate = true;
        }

        private void glControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e) {
            //This happens when initialized and when resized
            Render();
        }

        /*
        private void OpenTkControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            System.Windows.Forms.KeyEventArgs e2 = e.ToWinforms();

            switch (e2.KeyData)
            {
                case System.Windows.Forms.Keys.W:
                    MainCamera.Move(new Vector2(0f, -10f));
                    break;
                case System.Windows.Forms.Keys.A:
                    MainCamera.Move(new Vector2(-10f, 0f));
                    break;
                case System.Windows.Forms.Keys.S:
                    MainCamera.Move(new Vector2(0f, 10f));
                    break;
                case System.Windows.Forms.Keys.D:
                    MainCamera.Move(new Vector2(10f, 0f));
                    break;

                case System.Windows.Forms.Keys.X:
                    MainCamera.Position = Vector2.Zero;
                    break;

                case System.Windows.Forms.Keys.T:
                    MainCamera.Scale = Vector2.Add(MainCamera.Scale, new Vector2(0.1f, 0.1f));
                    break;
                case System.Windows.Forms.Keys.G:
                    if (MainCamera.Scale.X > 0.2f && MainCamera.Scale.Y > 0.2f) {
                        //MainCamera.Move(new Vector2(-90f, 0f));
                        //MainCamera.Move(new Vector2(0f, -40f));

                        MainCamera.Scale = Vector2.Subtract(MainCamera.Scale, new Vector2(0.1f, 0.1f));
                    }
                    break;
                case System.Windows.Forms.Keys.Y:
                    MainCamera.Scale = new Vector2(1f, 1f);
                    break;

                default:
                    Console.WriteLine("KeyDown: " + e2.KeyData);
                    break;
            }

            DebugKeyboard();
            glControl.Invalidate();
        }
        */

        private void toolZoomIn_Click(object sender, RoutedEventArgs e) {
            if (virtualViewWant.Width - 200 < 0 || virtualViewWant.Height - 200 < 0)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X + 100, virtualViewWant.Y + 100, virtualViewWant.Width - 200, virtualViewWant.Height - 200);
            virtualRequireViewportUpdate = true;

            DebugKeyboard();
            glControl.Invalidate();
        }

        private void toolZoomOut_Click(object sender, RoutedEventArgs e) {
            virtualViewWant = new Rectangle(virtualViewWant.X - 100, virtualViewWant.Y - 100, virtualViewWant.Width + 200, virtualViewWant.Height + 200);
            virtualRequireViewportUpdate = true;

            DebugKeyboard();
            glControl.Invalidate();
        }

        private void glControl_Load(object sender, EventArgs e) {
            InitTextures();
            RefreshVirtual();

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
                if (screen.Bounds.IntersectsWith(new System.Drawing.Rectangle((int)this.Left, (int)this.Top, (int)this.Width, (int)this.Height))) {
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
            if (!controlEnabled)
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
            if (currentScreen == null)
                return;

            windowActivatedMouseMove = false;

            Vector2 point = MainCamera.ScreenToWorldCoordinates(new Vector2((float)(e.X / scaleX), (float)(e.Y / scaleY)), virtualViewNeed.X, virtualViewNeed.Y);

            DebugMouseEvent((int)point.X, (int)point.Y);

            //!! Need to check if the position is valid before sending

            bool valid = false;
            foreach(RCScreen screen in listScreen) {
                if(screen.rect.Contains((int)point.X, (int)point.Y)) {
                    valid = true;

                    if (currentScreen != screen) {
                        currentScreen = screen;
                        rc.ChangeScreen(screen.screen_id);
                    }

                    break;
                }
            }

            if (!controlEnabled || !valid || !this.IsActive)
                return;

            rc.SendMousePosition((int)point.X, (int)point.Y);
        }

        private void HandleMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!controlEnabled)
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

                    if (keykaseya.IsMod) {
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
            string strMousePos = string.Format("X: {0}, Y: {1}", X, Y);

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

            glControl.Invalidate();
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

            // !! TEST
            float currentAspectRatio = (float)glControl.Width / (float)glControl.Height;
            float targetAspectRatio = (float)virtualViewWant.Width / (float)virtualViewWant.Height;

            strKeyboard = string.Format("Need: {0}\r\nWant: {1}\r\nScale: {2:0.###}, {3:0.###}\r\nCam Trans: {4}\r\nCam Scale: {5}\r\nRatio: {6}\r\nor {7}",
                virtualViewNeed.ToString(), virtualViewWant.ToString(),
                scaleX, scaleY, MainCamera.Position.ToString(), MainCamera.Scale.X, currentAspectRatio, targetAspectRatio);
            // !! TEST

            using (Graphics gfx = Graphics.FromImage(overlay2dKeyboard)) {
                gfx.Clear(System.Drawing.Color.Transparent);

                using (GraphicsPath gp = new GraphicsPath())
                using (System.Drawing.Pen outline = new System.Drawing.Pen(System.Drawing.Color.Black, 4) { LineJoin = LineJoin.Round }) //outline width=1
                using (StringFormat sf = new StringFormat())
                using (System.Drawing.Brush foreBrush = new SolidBrush(System.Drawing.Color.Lime)) {
                    gp.AddString(strKeyboard, arial.FontFamily, (int)arial.Style, 12, new PointF(0, 0), sf); //12=arial.Size
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
