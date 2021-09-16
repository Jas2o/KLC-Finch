﻿using KLC;
using LibKaseya;
using NTR;
using Ookii.Dialogs.Wpf;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KLC_Finch {

    /// <summary>`
    /// Interaction logic for WindowViewerOpenGL.xaml
    /// </summary>
    public partial class WindowViewerOpenGL : WindowViewer {
        public Settings Settings;

        private const int overlayHeight = 100;
        private const int overlayWidth = 400;
        private static Font arial = new Font("Arial", 32);
        private ScreenStatus screenStatus;
        private bool requiredApproval;
        private bool autotypeAlwaysConfirmed;
        private string clipboard = "";
        private readonly ClipBoardMonitor clipboardMon;
        private bool controlEnabled = false;
        private TSSession currentTSSession = null;
        private readonly FPSCounter fpsCounter;
        private double fpsLast;
        private int fragment_shader_object = 0;
        private string glVersion;
        private bool keyDownWin;
        private int legacyVirtualWidth, legacyVirtualHeight;
        public List<RCScreen> ListScreen { get; private set; }
        private readonly List<TSSession> listTSSession = new List<TSSession>();
        private readonly RCScreen legacyScreen;
        private readonly object lockFrameBuf = new object();
        private int m_shader_multiplyColor = 0;
        private readonly int[] m_shader_sampler = new int[3];
        private readonly Camera MainCamera;
        private Bitmap overlay2dControlOff;
        private Bitmap overlay2dDisconnected;
        private Bitmap overlay2dKeyboard;
        private Bitmap overlay2dMouse;
        private RCScreen previousScreen = null;
        private double scaleX, scaleY;
        private int shader_program = 0;
        private TextureCursor textureCursor = null;
        private TextureScreen textureLegacy;
        private int textureOverlay2dControlOff;
        private int textureOverlay2dDisconnected;
        private int textureOverlay2dKeyboard;
        private int textureOverlay2dMouse;
        private byte[] textureOverlayDataControlOff;
        private byte[] textureOverlayDataDisconnected;
        private byte[] textureOverlayDataKeyboard;
        private byte[] textureOverlayDataMouse;
        private readonly Timer timerHealth;
        private readonly Timer timerClipboard;
        private bool useMultiScreen;
        private bool useMultiScreenOverview;
        private int VBOmouse, VBOkeyboard, VBOtop, VBOcenter;
        private int VBOScreen;
        private Vector2[] vertBufferMouse, vertBufferKeyboard, vertBufferTop, vertBufferCenter;
        private Vector2[] vertBufferScreen;
        private int vertex_shader_object = 0;
        private Rectangle virtualCanvas, virtualViewWant, virtualViewNeed;
        private bool virtualRequireViewportUpdate = false;
        private int vpX, vpY;

        private readonly Agent.OSProfile endpointOS;
        private readonly string endpointLastUser;
        private string[] arrAdmins = new string[] { "administrator", "brandadmin", "adminc", "company" };

        private readonly WindowScreens winScreens;
        private readonly KeyboardHook keyHook;
        private bool ssKeyHookAllow;
        private bool ssClipboardSync;

        public WindowViewerOpenGL(IRemoteControl rc, int virtualWidth = 1920, int virtualHeight = 1080, Agent.OSProfile endpointOS = Agent.OSProfile.Other, string endpointLastUser = "") {
            InitializeComponent();
            ListScreen = new List<RCScreen>();
            winScreens = new WindowScreens();
            keyHook = new KeyboardHook();
            keyHook.KeyDown += KeyHook_KeyDown;
            keyHook.KeyUp += KeyHook_KeyUp;
            toolVersion.Header = "Build date: " + App.Version;

            this.endpointOS = endpointOS;
            this.endpointLastUser = endpointLastUser;

            Settings = App.Settings;

            DpiScale dpiScale = System.Windows.Media.VisualTreeHelper.GetDpi(this);
            this.Width = Settings.RemoteControlWidth / dpiScale.PixelsPerDip;
            this.Height = Settings.RemoteControlHeight / dpiScale.PixelsPerDip;
            LoadSettings(true);

            if (endpointOS == Agent.OSProfile.Mac) {
                toolBlockMouseKB.Visibility = Visibility.Collapsed;
                if (Settings.MacSwapCtrlWin)
                    toolKeyWin.Visibility = Visibility.Collapsed;
            }
            toolBlockScreen.Visibility = Visibility.Collapsed;

            this.rc = rc;

            state = new RCstate(null);
            SetControlEnabled(false, false); //Just for the visual

            //Multi-screen
            MainCamera = new Camera(Vector2.Zero);
            //Legacy
            if (!useMultiScreen)
                SetVirtual(0, 0, virtualWidth, virtualHeight);

            clipboardMon = new ClipBoardMonitor();
            clipboardMon.OnUpdate += SyncClipboard;

            //if (rcSessionId != null) {
            timerHealth = new System.Timers.Timer(1000);
            timerHealth.Elapsed += CheckHealth;
            timerHealth.Start();
            //}
            timerClipboard = new System.Timers.Timer(500); //Time changes
            timerClipboard.Elapsed += TimerClipboard_Elapsed;
            fpsCounter = new FPSCounter();

            legacyScreen = new RCScreen("Legacy", "Legacy", 800, 600, 0, 0);

            //txtDebugLeft.Text = "";
            //txtDebugRight.Text = "";
            //txtRcConnecting.Visibility = Visibility.Visible; //Default
            //txtRcFrozen.Visibility = Visibility.Collapsed;

            WindowUtilities.ActivateWindow(this);
        }

        private void WindowViewerV2_SizeChanged(object sender, SizeChangedEventArgs e) {
            Console.WriteLine(e.NewSize.ToString());
        }

        private void glControl_Resize(object sender, EventArgs e) {
            RefreshVirtual();
            virtualRequireViewportUpdate = true;
        }

        private void InitOverlayTexture(ref Bitmap overlay2d, ref int textureOverlay2d, int overlayW = overlayWidth, int overlayH = overlayHeight) {
            overlay2d = new Bitmap(overlayW, overlayH);
            textureOverlay2d = GL.GenTexture();

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureOverlay2d);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)All.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)All.Linear);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, overlay2d.Width, overlay2d.Height, 0,
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, IntPtr.Zero); // just allocate memory, so we can update efficiently using TexSubImage2D
        }

        private void glControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e) {
            //This happens when initialized and when resized
            Render();
        }

        private void glControl_Load(object sender, EventArgs e) {
            if (rc == null)
                return;

            glVersion = GL.GetString(StringName.Version);

            CreateShaders(Shaders.yuvtorgb_vertex, Shaders.yuvtorgb_fragment, out vertex_shader_object, out fragment_shader_object, out shader_program);
            m_shader_sampler[0] = GL.GetUniformLocation(shader_program, "y_sampler");
            m_shader_sampler[1] = GL.GetUniformLocation(shader_program, "u_sampler");
            m_shader_sampler[2] = GL.GetUniformLocation(shader_program, "v_sampler");
            m_shader_multiplyColor = GL.GetUniformLocation(shader_program, "multiplyColor");

            InitTextures();
            RefreshVirtual();

            glControl.Paint += glControl_Paint;
            glControl.Resize += glControl_Resize;
            glControl.MouseMove += HandleMouseMove;
            glControl.MouseDown += HandleMouseDown;
            glControl.MouseUp += HandleMouseUp;
            glControl.MouseWheel += HandleMouseWheel;
        }

        private void Render() {
            glControl.MakeCurrent();

            if (useMultiScreen)
                RenderStartMulti();
            else
                RenderStartLegacy();

            //--

            //GL.UseProgram(0); //No shader for RGB

            // Setup new textures, not actually render
            //lock (lockFrameBuf) {
            foreach (RCScreen screen in ListScreen) {
                if (screen.Texture == null)
                    screen.Texture = new TextureScreen(rc.DecodeMode);
                else
                    screen.Texture.RenderNew(m_shader_sampler);
            }
            if (useMultiScreen) {
                if (textureCursor == null) {
                    textureCursor = new TextureCursor();
                } else
                    textureCursor.RenderNew();
            }
            if (!useMultiScreen) {
                if (textureLegacy != null)
                    textureLegacy.RenderNew(m_shader_sampler);
            }
            //}

            switch (state.connectionStatus) {
                case ConnectionStatus.FirstConnectionAttempt:
                    GL.ClearColor(System.Drawing.Color.SlateGray);
                    break;

                case ConnectionStatus.Connected:
                    if (controlEnabled)
                        GL.ClearColor(Color.FromArgb(255, 20, 20, 20));
                    else
                        GL.ClearColor(System.Drawing.Color.MidnightBlue);
                    break;

                case ConnectionStatus.Disconnected:
                    GL.ClearColor(System.Drawing.Color.Maroon);
                    break;
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (useMultiScreen) {
                foreach (RCScreen screen in ListScreen) {
                    if (screen.Texture != null) {
                        Color multiplyColor;
                        if (screen == state.CurrentScreen)
                            multiplyColor = Color.White;
                        else if (controlEnabled || virtualViewWant == virtualCanvas) //In overview, or it's on the edge of focused screen
                            multiplyColor = Color.Gray;
                        else
                            multiplyColor = Color.Cyan;

                        if (!screen.Texture.Render(shader_program, m_shader_sampler, m_shader_multiplyColor, multiplyColor)) {
                            GL.Disable(EnableCap.Texture2D);
                            //GL.UseProgram(0);
                            GL.Color3(Color.DimGray);

                            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.Begin(PrimitiveType.Polygon);
                            GL.PointSize(5f);
                            GL.LineWidth(5f);

                            GL.Vertex2(screen.rectFixed.Left, screen.rectFixed.Bottom);
                            GL.Vertex2(screen.rectFixed.Left, screen.rectFixed.Top);
                            GL.Vertex2(screen.rectFixed.Right, screen.rectFixed.Top);
                            GL.Vertex2(screen.rectFixed.Right, screen.rectFixed.Bottom);

                            //GL.Vertex2(vertBufferScreen[0].X, vertBufferScreen[0].Y);

                            GL.End();
                        }
                    }
                }

                if (Settings.MultiShowCursor) {
                    if (textureCursor != null) {
                        GL.Color3(Color.White);
                        textureCursor.Render();
                    }

                    /*
                    if(rectCursor != null) {
                        GL.Disable(EnableCap.Texture2D);
                        GL.Color3(Color.Yellow);

                        //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                        GL.Begin(PrimitiveType.Polygon);
                        GL.PointSize(5f);
                        GL.LineWidth(5f);

                        GL.Vertex2(rectCursor.Left, rectCursor.Bottom);
                        GL.Vertex2(rectCursor.Left, rectCursor.Top);
                        GL.Vertex2(rectCursor.Right, rectCursor.Top);
                        GL.Vertex2(rectCursor.Right, rectCursor.Bottom);

                        //GL.Vertex2(vertBufferScreen[0].X, vertBufferScreen[0].Y);

                        GL.End();
                    }
                    */
                }
            } else {
                //Legacy behavior
                if (!textureLegacy.Render(shader_program, m_shader_sampler, m_shader_multiplyColor, Color.White)) {
                    GL.Disable(EnableCap.Texture2D);
                    GL.UseProgram(0);
                    GL.Color3(Color.DimGray);

                    //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                    GL.Begin(PrimitiveType.Polygon);
                    GL.PointSize(5f);
                    GL.LineWidth(5f);

                    GL.Vertex2(0, legacyVirtualHeight);
                    GL.Vertex2(0, 0);
                    GL.Vertex2(legacyVirtualWidth, 0);
                    GL.Vertex2(legacyVirtualWidth, legacyVirtualHeight);

                    //GL.Vertex2(vertBufferScreen[0].X, vertBufferScreen[0].Y);

                    GL.End();
                }
            }

            //--

            GL.Enable(EnableCap.Texture2D);

            #region Overlay

            if (!useMultiScreen)
                GL.Viewport(0, 0, glControl.Width, glControl.Height);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, glControl.Width, glControl.Height, 0, MainCamera.ZNear, MainCamera.ZFar); //Test

            /*
            if (overlayNewMouse) {
                GL.ActiveTexture(TextureUnit.Texture0);
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
                GL.ActiveTexture(TextureUnit.Texture0);
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
            */

            GL.Color3(Color.White);
            GL.UseProgram(0);
            if (Settings.DisplayOverlayMouse) {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dMouse);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOmouse);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferMouse.Length / 2);
            }

            if (Settings.DisplayOverlayKeyboardOther || Settings.DisplayOverlayKeyboardMod) {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dKeyboard);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOkeyboard);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferKeyboard.Length / 2);
            }

            if (state.connectionStatus == ConnectionStatus.Disconnected) {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dDisconnected);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOcenter);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferCenter.Length / 2);
            } else if (state.connectionStatus == ConnectionStatus.Connected && !controlEnabled) {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dControlOff);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOtop);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferTop.Length / 2);
            }

            #endregion Overlay

            //--

            glControl.SwapBuffers();
        }

        private void TimerClipboard_Elapsed(object sender, ElapsedEventArgs e) {
            Dispatcher.Invoke((Action)delegate {
                toolClipboardSend.Background = System.Windows.Media.Brushes.Transparent;
                toolClipboardGet.Background = System.Windows.Media.Brushes.Transparent;
            });
            timerClipboard.Stop();
        }

        private void KeyHook_KeyUp(KeyboardHook.VKeys key) {
            Window_KeyUp2(new System.Windows.Forms.KeyEventArgs((System.Windows.Forms.Keys)key));
        }

        private void KeyHook_KeyDown(KeyboardHook.VKeys key) {
            Window_PreviewKeyDown2(new System.Windows.Forms.KeyEventArgs((System.Windows.Forms.Keys)key));
        }

        private void LoadSettings(bool isStart = false) {
            if (isStart) {
                useMultiScreen = Settings.StartMultiScreen;

                if (useMultiScreen) {
                    /*
                    if (Settings.StartControlEnabled)
                        PositionCameraToCurrentScreen();
                    else
                        ChangeViewToOverview();
                    */
                    toolScreenMode.Content = "Multi";
                    toolScreenOverview.Visibility = Visibility.Visible;
                    toolZoomIn.Visibility = Visibility.Visible;
                    toolZoomOut.Visibility = Visibility.Visible;
                } else {
                    toolScreenMode.Content = "Legacy";
                    toolScreenOverview.Visibility = Visibility.Collapsed;
                    toolZoomIn.Visibility = Visibility.Collapsed;
                    toolZoomOut.Visibility = Visibility.Collapsed;
                }
                //SetControlEnabled(Settings.StartControlEnabled, true);
            }

            autotypeAlwaysConfirmed = Settings.AutotypeSkipLengthCheck;
            ssKeyHookAllow = Settings.KeyboardHook;
            KeyHookSet(false);

            if (Settings.ClipboardSync == 2 && (endpointOS == Agent.OSProfile.Server || arrAdmins.Contains(endpointLastUser.ToLower()))) {
                //Server/Admin only
                ssClipboardSync = true;
            } else {
                ssClipboardSync = (Settings.ClipboardSync == 1);
            }

            // This repetition needs to be fixed
            toolClipboardSync.Visibility = (ssClipboardSync ? Visibility.Visible : Visibility.Collapsed);
            toolClipboardReceiveOnly.Visibility = !ssClipboardSync ? Visibility.Visible : Visibility.Collapsed;
        }

        private void KeyHookSet(bool canCheckKeyboard = false) {
            if (canCheckKeyboard) {
                ssKeyHookAllow = Keyboard.IsKeyToggled(Key.Scroll) || Settings.KeyboardHook;
                if (!IsActive)
                    ssKeyHookAllow = false;
            }

            if (!controlEnabled || !ssKeyHookAllow) {
                if (keyHook.IsActive)
                    keyHook.Uninstall();
            } else {
                if (!keyHook.IsActive)
                    keyHook.Install();
            }

            //if (canCheckKeyboard) //More like canUpdateUI
            //txtRcHookOn.Visibility = (keyHook.IsActive && Settings.DisplayOverlayKeyboardHook ? Visibility.Visible : Visibility.Collapsed);
        }

        public override void ClearApproval() {
            Dispatcher.Invoke((Action)delegate {
                txtRcNotify.Visibility = Visibility.Collapsed;
            });
        }

        public override void LoadCursor(int cursorX, int cursorY, int cursorWidth, int cursorHeight, int cursorHotspotX, int cursorHotspotY, byte[] remaining) {
            if (textureCursor != null)
                textureCursor.Load(new Rectangle(cursorX, cursorY, cursorWidth, cursorHeight), remaining);
        }

        public override void LoadTexture(int width, int height, Bitmap decomp) {
            if (screenStatus == ScreenStatus.Preparing)
                return;

            if (useMultiScreen) {

                #region Multi-Screen

                RCScreen scr = null;
                if (state.CurrentScreen != null && state.CurrentScreen.rect.Width == width && state.CurrentScreen.rect.Height == height)
                    scr = state.CurrentScreen;
                else if (state.CurrentScreen != null && state.CurrentScreen.rectFixed.Width == width && state.CurrentScreen.rectFixed.Height == height)
                    scr = state.CurrentScreen;
                else if (previousScreen != null && previousScreen.rect.Width == width && previousScreen.rect.Height == height)
                    scr = previousScreen;
                else if (previousScreen != null && previousScreen.rectFixed.Width == width && previousScreen.rectFixed.Height == height)
                    scr = previousScreen;
                else {
                    List<RCScreen> scrMatch = ListScreen.FindAll(x => x.rect.Width == width && x.rect.Height == height);
                    List<RCScreen> scrMatchFixed = ListScreen.FindAll(x => x.rectFixed.Width == width && x.rectFixed.Height == height);
                    List<RCScreen> scrMatchHalf = ListScreen.FindAll(x => x.rectFixed.Width == width / 2 && x.rectFixed.Height == height / 2);
                    if (scrMatch.Count == 1) {
                        scr = scrMatch[0];
                    } else if (scrMatchFixed.Count == 1) {
                        scr = scrMatchFixed[0];
                    } else if (scrMatchHalf.Count == 1) {
                        //Console.WriteLine("Mac with Retina display?");
                        scr = scrMatchHalf[0];
                        legacyVirtualWidth = scr.rectFixed.Width = width;
                        legacyVirtualHeight = scr.rectFixed.Height = height;
                        PositionCameraToCurrentScreen();
                    } else {
                        //Console.WriteLine("Forced switch from Multi-Screen to Legacy");
                        if (screenStatus == ScreenStatus.Stable) {
                            SwitchToLegacyRendering();
                            LoadTexture(width, height, decomp);
                        }
                        return;
                    }
                }

                if (scr == null) {
                    //Console.WriteLine("[LoadTexture] No matching RCScreen for screen ID: " + screenID);
                    //listScreen might be empty
                    return;
                }

                if (scr.rect.Width != width || scr.rect.Height != height) {
                    scr.rect.Width = width;
                    scr.rect.Height = height;
                }

                if (scr.Texture != null)
                    scr.Texture.Load(scr.rect, decomp);
                else {
                    Dispatcher.Invoke((Action)delegate {
                        if (scr.CanvasImage == null)
                            scr.CanvasImage = new System.Windows.Controls.Image();
                        scr.CanvasImage.Width = width;
                        scr.CanvasImage.Height = height;

                        scr.SetCanvasImage(decomp);
                    });
                }
                state.socketAlive = true;

                #endregion Multi-Screen
            } else {

                #region Legacy

                if (state.CurrentScreen == null)
                    return;

                if (legacyVirtualWidth != width || legacyVirtualHeight != height) {
#if DEBUG
                    Console.WriteLine("[LoadTexture:Legacy] Virtual resolution did not match texture received.");
#endif
                    SetVirtual(0, 0, width, height);

                    try {
                        state.CurrentScreen.rect.Width = width;
                        state.CurrentScreen.rect.Height = height;
                        //This is a sad attempt a fixing a problem when changing left monitor's size.
                        //However if changing a middle monitor, the right monitor will break.
                        //The reconnect button can otherwise be used, or perhaps a multimonitor/scan feature can be added to automatically detect and repair the list of screens.
                        if (state.CurrentScreen.rect.X < 0)
                            state.CurrentScreen.rect.X = width * -1;
                    } catch (Exception ex) {
                        Console.WriteLine("[LoadTexture:Legacy] " + ex.ToString());
                    }
                }

                if (textureLegacy != null)
                    textureLegacy.Load(new Rectangle(0, 0, width, height), decomp);
                else {
                    Dispatcher.Invoke((Action)delegate {
                        if (legacyScreen.CanvasImage == null)
                            legacyScreen.CanvasImage = new System.Windows.Controls.Image();
                        legacyScreen.CanvasImage.Height = width;
                        legacyScreen.CanvasImage.Width = height;

                        legacyScreen.SetCanvasImage(decomp);
                    });
                }

                /*
                textureLegacyWidth = width;
                textureLegacyHeight = height;

                BitmapData data = decomp.LockBits(new System.Drawing.Rectangle(0, 0, decomp.Width, decomp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format24bppRgb);

                if (textureLegacyData != null && textureLegacyData.Length != Math.Abs(data.Stride * data.Height)) {
                    virtualRequireViewportUpdate = true;
                    Console.WriteLine("[LoadTexture:Legacy] Array needs to be resized");
                }

                if (textureLegacyData == null || virtualRequireViewportUpdate) {
                    Console.WriteLine("Rebuilding Legacy Texture Buffer");
                    textureLegacyData = new byte[Math.Abs(data.Stride * data.Height)];
                }
                Marshal.Copy(data.Scan0, textureLegacyData, 0, textureLegacyData.Length); //This can fail with re-taking over private remote control
                decomp.UnlockBits(data);

                textureLegacyNew = true;
                */
                state.socketAlive = true;

                #endregion Legacy
            }
            //}

            fpsLast = fpsCounter.GetFPS();
            screenStatus = ScreenStatus.Stable;
            glControl.Invalidate();
        }

        public override void LoadTextureRaw(byte[] buffer, int width, int height, int stride) {
            if (screenStatus == ScreenStatus.Preparing || width * height <= 0)
                return;

            //lock (lockFrameBuf) {
            if (useMultiScreen) {

                #region Multi-Screen

                RCScreen scr = null;
                if (state.CurrentScreen != null && state.CurrentScreen.rect.Width == width && state.CurrentScreen.rect.Height == height)
                    scr = state.CurrentScreen;
                else if (state.CurrentScreen != null && state.CurrentScreen.rectFixed.Width == width && state.CurrentScreen.rectFixed.Height == height)
                    scr = state.CurrentScreen;
                else if (previousScreen != null && previousScreen.rect.Width == width && previousScreen.rect.Height == height)
                    scr = previousScreen;
                else if (previousScreen != null && previousScreen.rectFixed.Width == width && previousScreen.rectFixed.Height == height)
                    scr = previousScreen;
                else {
                    List<RCScreen> scrMatch = ListScreen.FindAll(x => x.rect.Width == width && x.rect.Height == height);
                    List<RCScreen> scrMatchFixed = ListScreen.FindAll(x => x.rectFixed.Width == width && x.rectFixed.Height == height);
                    List<RCScreen> scrMatchHalf = ListScreen.FindAll(x => x.rectFixed.Width == width / 2 && x.rectFixed.Height == height / 2);
                    if (scrMatch.Count == 1) {
                        scr = scrMatch[0];
                    } else if (scrMatchFixed.Count == 1) {
                        scr = scrMatchFixed[0];
                    } else if (scrMatchHalf.Count == 1) {
                        //Console.WriteLine("Mac with Retina display?");
                        scr = scrMatchHalf[0];
                        legacyVirtualWidth = scr.rectFixed.Width = width;
                        legacyVirtualHeight = scr.rectFixed.Height = height;
                        PositionCameraToCurrentScreen();
                    } else {
                        if (screenStatus == ScreenStatus.Stable) {
                            //Console.WriteLine("Forced switch from Multi-Screen to Legacy");
                            SwitchToLegacyRendering();
                            LoadTextureRaw(buffer, width, height, stride);
                        }
                        return;
                    }
                }

                if (scr == null) {
                    //Console.WriteLine("[LoadTexture] No matching RCScreen for screen ID: " + screenID);
                    //listScreen might be empty
                    return;
                }

                if (scr.rect.Width != width || scr.rect.Height != height) {
                    scr.rect.Width = width;
                    scr.rect.Height = height;
                }

                if (scr.Texture != null)
                    scr.Texture.LoadRaw(scr.rect, buffer, stride);
                else {
                    //Canvas
                    Dispatcher.Invoke((Action)delegate {
                        if (scr.CanvasImage == null)
                            scr.CanvasImage = new System.Windows.Controls.Image();
                        scr.CanvasImage.Width = width;
                        scr.CanvasImage.Height = height;

                        scr.SetCanvasImageBW(width, height, stride, buffer);
                    });
                }

                state.socketAlive = true;

                #endregion Multi-Screen
            } else {

                #region Legacy

                if (state.CurrentScreen == null)
                    return;

                if (legacyVirtualWidth != width || legacyVirtualHeight != height) {
#if DEBUG
                    Console.WriteLine("[LoadTexture:Legacy] Virtual resolution did not match texture received.");
#endif
                    SetVirtual(0, 0, width, height);

                    try {
                        state.CurrentScreen.rect.Width = state.CurrentScreen.rectFixed.Width = width;
                        state.CurrentScreen.rect.Height = state.CurrentScreen.rectFixed.Height = height;
                        ////This is a sad attempt a fixing a problem when changing left monitor's size.
                        ////However if changing a middle monitor, the right monitor will break.
                        ////The reconnect button can otherwise be used, or perhaps a multimonitor/scan feature can be added to automatically detect and repair the list of screens.
                        //if (currentScreen.rect.X < 0)
                        //currentScreen.rect.X = width * -1;
                    } catch (Exception ex) {
                        Console.WriteLine("[LoadTexture:Legacy] " + ex.ToString());
                    }
                }

                textureLegacy.LoadRaw(new Rectangle(0, 0, width, height), buffer, stride);

                state.socketAlive = true;

                #endregion Legacy
            }
            //}

            fpsLast = fpsCounter.GetFPS();
            screenStatus = ScreenStatus.Stable;
            glControl.Invalidate();
        }

        public override void NotifySocketClosed(string sessionId) {
            if (sessionId == "/control/agent") {
            } else if (state.sessionId != sessionId)
                return;

            state.socketAlive = false;
            state.connectionStatus = ConnectionStatus.Disconnected;

            glControl.Invalidate();
        }

        public override void ReceiveClipboard(string content) {
            if (clipboard == content) {
                Dispatcher.Invoke((Action)delegate {
                    if (toolClipboardGet.Background == System.Windows.Media.Brushes.Transparent) {
                        toolClipboardGet.Background = System.Windows.Media.Brushes.PaleGreen;
                        timerClipboard.Start();
                    }
                });
                return;
            }

            clipboard = content;
            Dispatcher.Invoke((Action)delegate {
                toolClipboardGetText.Text = clipboard.Truncate(50);
                if (clipboard.Length > 0)
                    Clipboard.SetDataObject(clipboard);
                else
                    Clipboard.Clear();

                toolClipboardGet.Background = System.Windows.Media.Brushes.GreenYellow;
                timerClipboard.Start();
            });
        }

        public override void SetApprovalAndSpecialNote(Enums.NotifyApproval rcNotify, int machineShowToolTip, string machineNote, string machineNoteLink) {
            if ((int)rcNotify < 3) {
                txtRcNotify.Visibility = Visibility.Collapsed;
            } else {
                requiredApproval = true;
                toolReconnect.Header = "Reconnect (reapproval required)";
                //txtRcConnecting.Visibility = Visibility.Collapsed;
            }

            if (machineShowToolTip > 0 || machineNote.Length > 0 || machineNoteLink != null)
                toolMachineNote.Visibility = Visibility.Visible;
            else
                toolMachineNote.Visibility = Visibility.Collapsed;

            if (machineShowToolTip > 0 && Enum.IsDefined(typeof(controlDashboard.Badge), machineShowToolTip))
                toolMachineNote.Content = Enum.GetName(typeof(controlDashboard.Badge), machineShowToolTip);

            toolMachineNoteText.Header = machineNote;
            toolMachineNoteText.Visibility = (machineNote.Length == 0 ? Visibility.Collapsed : Visibility.Visible);

            toolMachineNoteLink.Header = machineNoteLink;
            toolMachineNoteLink.Visibility = (machineNoteLink == null ? Visibility.Collapsed : Visibility.Visible);
        }

        public void SetCanvas(int virtualX, int virtualY, int virtualWidth, int virtualHeight) { //More like lowX, lowY, highX, highY
            //OpenGL
            if (useMultiScreen) {
                virtualCanvas = new Rectangle(virtualX, virtualY, Math.Abs(virtualX) + virtualWidth, Math.Abs(virtualY) + virtualHeight);
                SetVirtual(virtualX, virtualY, virtualCanvas.Width, virtualCanvas.Height);
            } else {
                virtualCanvas = new Rectangle(0, 0, virtualWidth, virtualHeight);
                SetVirtual(0, 0, virtualWidth, virtualHeight);
            }

            virtualRequireViewportUpdate = true;
        }

        public override void SetControlEnabled(bool value, bool isStart = false) {
            if (isStart) {
                controlEnabled = Settings.StartControlEnabled;
                if (Settings.StartMultiScreen && controlEnabled)
                    PositionCameraToCurrentScreen();
                else
                    useMultiScreenOverview = true;
            } else
                controlEnabled = value;

            Dispatcher.Invoke((Action)delegate {
                if (controlEnabled)
                    toolToggleControl.Content = "Control Enabled";
                else
                    toolToggleControl.Content = "Control Disabled";
                toolToggleControl.FontWeight = (controlEnabled ? FontWeights.Normal : FontWeights.Bold);

                toolSendCtrlAltDel.IsEnabled = controlEnabled;

                KeyHookSet(true);
            });

            glControl.Invalidate();
        }

        public override void SetSessionID(string sessionId) {
            state.sessionId = sessionId;

            if (sessionId != null) {
                state.socketAlive = true;
                //connectionStatus = ConnectionStatus.Connected;
            }
        }

        public override void SetTitle(string title, bool modePrivate) {
            this.Title = title + "::" + (modePrivate ? "Private" : "Shared");
            if (modePrivate)
                toolReconnect.Header = "Reconnect (lose private session)";
        }

        public void SetVirtual(int virtualX, int virtualY, int virtualWidth, int virtualHeight) {
            if (useMultiScreen) {
                virtualViewWant = new Rectangle(virtualX, virtualY, virtualWidth, virtualHeight);
            } else {
                this.legacyVirtualWidth = virtualWidth;
                this.legacyVirtualHeight = virtualHeight;
                virtualCanvas = virtualViewWant = new Rectangle(0, 0, virtualWidth, virtualHeight);
            }

            virtualRequireViewportUpdate = true;
        }

        public override void UpdateLatency(long ms) {
            state.lastLatency = ms;

            //Dipatcher.Invoke((Action)delegate {
            //toolLatency.Content = string.Format("{0} ms", ms);
            //});
        }

        private void ChangeViewToOverview() {
            if (!useMultiScreen)
                return;

            useMultiScreenOverview = true;

            int lowestX = 0;
            int lowestY = 0;
            int highestX = 0;
            int highestY = 0;
            foreach (RCScreen screen in ListScreen) {
                lowestX = Math.Min(screen.rectFixed.X, lowestX);
                lowestY = Math.Min(screen.rectFixed.Y, lowestY);
                highestX = Math.Max(screen.rectFixed.X + screen.rectFixed.Width, highestX);
                highestY = Math.Max(screen.rectFixed.Y + screen.rectFixed.Height, highestY);
            }

            SetCanvas(lowestX, lowestY, highestX, highestY);

            //--

            //MainCamera.Rotation = 0f;
            MainCamera.Position = Vector2.Zero;
            MainCamera.Scale = new Vector2(1f, 1f);
            //DebugKeyboard();

            virtualViewWant = virtualCanvas;
            virtualRequireViewportUpdate = true;
            glControl.Invalidate();
        }

        private void CheckHealth(object sender, ElapsedEventArgs e) {
            return;
        }

        #region OpenGL

        private void CreateShaders(string vs, string fs, out int vertexObject, out int fragmentObject, out int program) {
            vertexObject = GL.CreateShader(ShaderType.VertexShader);
            fragmentObject = GL.CreateShader(ShaderType.FragmentShader);

            // Compile vertex shader
            GL.ShaderSource(vertexObject, vs);
            GL.CompileShader(vertexObject);
            GL.GetShaderInfoLog(vertexObject, out string info);
            GL.GetShader(vertexObject, ShaderParameter.CompileStatus, out int status_code);

            if (status_code != 1)
                throw new ApplicationException(info);

            // Compile vertex shader
            GL.ShaderSource(fragmentObject, fs);
            GL.CompileShader(fragmentObject);
            GL.GetShaderInfoLog(fragmentObject, out info);
            GL.GetShader(fragmentObject, ShaderParameter.CompileStatus, out status_code);

            if (status_code != 1)
                throw new ApplicationException(info);

            program = GL.CreateProgram();
            GL.AttachShader(program, fragmentObject);
            GL.AttachShader(program, vertexObject);

            GL.LinkProgram(program);
            GL.UseProgram(program);
        }

        private void GlControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            //Does this do anything anymore?
            RefreshVirtual();
            virtualRequireViewportUpdate = true;
        }

        private void HandleCanvasMouseWheel(object sender, MouseWheelEventArgs e) {
            if (!controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.SendMouseWheel(e.Delta);
        }

        private void HandleCanvasMouseUp(object sender, MouseButtonEventArgs e) {
            if (!controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            //if (rcCanvas.Contains(e.GetPosition((Canvas)sender))) {
            rc.SendMouseUp(e.ChangedButton);

            if (e.ChangedButton == MouseButton.Left)
                state.mouseHeldLeft = false;
            if (e.ChangedButton == MouseButton.Right)
                state.mouseHeldRight = false;

            DebugKeyboard();
            //}
        }

        private void InitTextures() {
            //FPS/Mouse/Keyboard
            InitOverlayTexture(ref overlay2dMouse, ref textureOverlay2dMouse);
            InitOverlayTexture(ref overlay2dKeyboard, ref textureOverlay2dKeyboard);
            InitOverlayTexture(ref overlay2dDisconnected, ref textureOverlay2dDisconnected, 400);
            InitOverlayTexture(ref overlay2dControlOff, ref textureOverlay2dControlOff, 400);

            #region Texture - Disconnected

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

            GL.ActiveTexture(TextureUnit.Texture0);
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

            #endregion Texture - Disconnected

            #region Texture - Control Off

            using (Graphics gfx = Graphics.FromImage(overlay2dControlOff)) {
                gfx.Clear(System.Drawing.Color.Transparent);

                using (GraphicsPath gp = new GraphicsPath())
                using (System.Drawing.Pen outline = new System.Drawing.Pen(Color.FromArgb(128, Color.Black), 4) { LineJoin = LineJoin.Round }) //outline width=1
                using (StringFormat sf = new StringFormat() { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                using (System.Drawing.Brush foreBrush = new SolidBrush(Color.FromArgb(128, Color.White))) {
                    gp.AddString("F2 or double-click to enable control", arial.FontFamily, (int)arial.Style, arial.Size, gfx.VisibleClipBounds, sf);
                    gfx.DrawPath(outline, gp);
                    gfx.FillPath(foreBrush, gp);
                }

                BitmapData data2 = overlay2dControlOff.LockBits(new System.Drawing.Rectangle(0, 0, overlay2dControlOff.Width, overlay2dControlOff.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                if (textureOverlayDataControlOff == null)
                    textureOverlayDataControlOff = new byte[Math.Abs(data2.Stride * data2.Height)];
                Marshal.Copy(data2.Scan0, textureOverlayDataControlOff, 0, textureOverlayDataControlOff.Length);

                overlay2dControlOff.UnlockBits(data2);
            }

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dControlOff);

            GL.TexImage2D(
                TextureTarget.Texture2D,
                0, //Level
                PixelInternalFormat.Rgba,
                overlay2dControlOff.Width,
                overlay2dControlOff.Height,
                0, //Border
                OpenTK.Graphics.OpenGL.PixelFormat.Bgra,
                PixelType.UnsignedByte,
                textureOverlayDataControlOff);

            #endregion Texture - Control Off

            textureLegacy = new TextureScreen(rc.DecodeMode);
            //InitLegacyScreenTexture();

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.Enable(EnableCap.Texture2D);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        private void PositionCameraToCurrentScreen() {
            if (!useMultiScreen)
                return;

            useMultiScreenOverview = false;

            //MainCamera.Rotation = 0f;
            MainCamera.Position = Vector2.Zero;
            MainCamera.Scale = new Vector2(1f, 1f);
            //DebugKeyboard();

            if (Settings.MultiAltFit) {
                bool adjustLeft = false;
                bool adjustUp = false;
                bool adjustRight = false;
                bool adjustDown = false;

                foreach (RCScreen screen in ListScreen) {
                    if (screen == state.CurrentScreen)
                        continue;

                    if (screen.rect.Right <= state.CurrentScreen.rect.Left)
                        adjustLeft = true;
                    if (screen.rect.Bottom <= state.CurrentScreen.rect.Top)
                        adjustUp = true;
                    if (screen.rect.Left >= state.CurrentScreen.rect.Right)
                        adjustRight = true;
                    if (screen.rect.Top >= state.CurrentScreen.rect.Bottom)
                        adjustDown = true;
                }

                SetVirtual(state.CurrentScreen.rectFixed.X - (adjustLeft ? 80 : 0),
                    state.CurrentScreen.rectFixed.Y - (adjustUp ? 80 : 0),
                    state.CurrentScreen.rectFixed.Width + (adjustLeft ? 80 : 0) + (adjustRight ? 80 : 0),
                    state.CurrentScreen.rectFixed.Height + (adjustUp ? 80 : 0) + (adjustDown ? 80 : 0));
            } else
                SetVirtual(state.CurrentScreen.rectFixed.X, state.CurrentScreen.rectFixed.Y, state.CurrentScreen.rectFixed.Width, state.CurrentScreen.rectFixed.Height);

            glControl.Invalidate();
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

            int leftCenter = (glControl.Width - 400) / 2;
            int topCenter = (glControl.Height - overlayHeight) / 2;
            vertBufferCenter = new Vector2[8] {
                new Vector2(leftCenter, topCenter + overlayHeight), new Vector2(0, 1),
                new Vector2(leftCenter + 400, topCenter + overlayHeight), new Vector2(1, 1),
                new Vector2(leftCenter + 400, topCenter), new Vector2(1, 0),
                new Vector2(leftCenter, topCenter), new Vector2(0, 0)
            };

            VBOcenter = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOcenter);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferCenter.Length), vertBufferCenter, BufferUsageHint.StaticDraw);

            vertBufferTop = new Vector2[8] {
                new Vector2(leftCenter, overlayHeight), new Vector2(0, 1),
                new Vector2(leftCenter + 400, overlayHeight), new Vector2(1, 1),
                new Vector2(leftCenter + 400, 0), new Vector2(1, 0),
                new Vector2(leftCenter, 0), new Vector2(0, 0)
            };

            VBOtop = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOtop);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferTop.Length), vertBufferTop, BufferUsageHint.StaticDraw);
        }

        private void RenderStartLegacy() {
            if (virtualRequireViewportUpdate) {
                RefreshVirtual();
                virtualRequireViewportUpdate = false;
            }

            int screenWidth = glControl.Width;
            int screenHeight = glControl.Height;

            float targetAspectRatio = (float)legacyVirtualWidth / (float)legacyVirtualHeight;

            int width = screenWidth;
            int height = (int)((float)width / targetAspectRatio/* + 0.5f*/);

            if (height > screenHeight) {
                //Pillarbox
                height = screenHeight;
                width = (int)((float)height * targetAspectRatio/* + 0.5f*/);
            }

            vpX = (screenWidth / 2) - (width / 2);
            vpY = (screenHeight / 2) - (height / 2);

            GL.Viewport(vpX, vpY, width, height);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, legacyVirtualWidth, legacyVirtualHeight, 0, -1, 1);//Upsidedown
            //GL.Ortho(0, legacyVirtualWidth, 0, legacyVirtualHeight, -1, 1);

            GL.MatrixMode(MatrixMode.Modelview);
            //GL.PushMatrix();

            //Now to calculate the scale considering the screen size and virtual size
            scaleX = (double)screenWidth / (double)legacyVirtualWidth;
            scaleY = (double)screenHeight / (double)legacyVirtualHeight;
            GL.Scale(scaleX, scaleY, 1.0f);

            GL.LoadIdentity();

            GL.Disable(EnableCap.DepthTest);
        }

        private void RenderStartMulti() {
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
        }

        #endregion OpenGL

        private void HandleMouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (rc == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            if (useMultiScreen) {
                Vector2 point = MainCamera.ScreenToWorldCoordinates(new Vector2((float)(e.X / scaleX), (float)(e.Y / scaleY)), virtualViewNeed.X, virtualViewNeed.Y);
                RCScreen screenPointingTo = GetScreenUsingMouse((int)point.X, (int)point.Y);
                if (screenPointingTo == null)
                    return;

                if (controlEnabled) {
                    if (virtualViewWant != virtualCanvas && screenPointingTo != state.CurrentScreen) {
                        FromGlChangeScreen(screenPointingTo, true);
                        return;
                    }
                } else {
                    if (e.Clicks == 2) {
                        SetControlEnabled(true);
                    } else if (e.Button == System.Windows.Forms.MouseButtons.Left) {
                        if (state.CurrentScreen != screenPointingTo) //Multi-Screen (Focused), Control Disabled, Change Screen
                            FromGlChangeScreen(screenPointingTo, false);
                        //Else
                        //We already changed the active screen by moving the mouse
                        PositionCameraToCurrentScreen();
                    }

                    return;
                }
            } else {
                //Use legacy behavior

                if (!controlEnabled) {
                    if (e.Clicks == 2)
                        SetControlEnabled(true);

                    return;
                }
            }

            if (e.Button == System.Windows.Forms.MouseButtons.Middle) {
                if (e.Clicks == 1) //Logitech bug
                    PerformAutotype();
            } else {
                if (state.windowActivatedMouseMove)
                    HandleMouseMove(sender, e);

                rc.SendMouseDown(e.Button);

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    state.mouseHeldLeft = true;
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    state.mouseHeldRight = true;

                DebugKeyboard();
            }
        }

        private void HandleMouseMove(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (state.CurrentScreen == null || rc == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            state.windowActivatedMouseMove = false;

            if (useMultiScreen) {
                Vector2 point = MainCamera.ScreenToWorldCoordinates(new Vector2((float)(e.X / scaleX), (float)(e.Y / scaleY)), virtualViewNeed.X, virtualViewNeed.Y);
                DebugMouseEvent((int)point.X, (int)point.Y);

                RCScreen screenPointingTo = GetScreenUsingMouse((int)point.X, (int)point.Y);
                if (screenPointingTo == null)
                    return;

                if (virtualViewWant == virtualCanvas && state.CurrentScreen.screen_id != screenPointingTo.screen_id) {
                    //We are in overview, change which screen gets texture updates
                    FromGlChangeScreen(screenPointingTo, false);

                    //previousScreen = currentScreen;
                    //currentScreen = screenPointingTo;
                    //rc.ChangeScreen(currentScreen.screen_id);
                }

                if (!controlEnabled || !this.IsActive)
                    return;

                rc.SendMousePosition((int)point.X, (int)point.Y);
            } else {
                //Legacy behavior
                if (!controlEnabled || !this.IsActive)
                    return;

                System.Drawing.Point legacyPoint = new System.Drawing.Point(e.X - vpX, e.Y - vpY);
                if (legacyPoint.X < 0 || legacyPoint.Y < 0)
                    if (legacyPoint.X < 0 || legacyPoint.Y < 0)
                        return;

                if (vpX > 0) {
                    legacyPoint.X = (int)(legacyPoint.X / scaleY);
                    legacyPoint.Y = (int)(legacyPoint.Y / scaleY);
                } else {
                    legacyPoint.X = (int)(legacyPoint.X / scaleX);
                    legacyPoint.Y = (int)(legacyPoint.Y / scaleX);
                }

                if (legacyPoint.X > legacyVirtualWidth || legacyPoint.Y > legacyVirtualHeight)
                    return;

                legacyPoint.X = legacyPoint.X + state.CurrentScreen.rect.X;
                legacyPoint.Y = legacyPoint.Y + state.CurrentScreen.rect.Y;

                DebugMouseEvent(legacyPoint.X, legacyPoint.Y);

                rc.SendMousePosition(legacyPoint.X, legacyPoint.Y);
            }
        }

        private void HandleMouseUp(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!controlEnabled || rc == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            if (glControl.ClientRectangle.Contains(e.Location)) {
                rc.SendMouseUp(e.Button);

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    state.mouseHeldLeft = false;
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    state.mouseHeldRight = false;

                DebugKeyboard();
            }
        }

        private void HandleMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!controlEnabled || rc == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.SendMouseWheel(e.Delta);
        }

        private void PerformAutotype() {
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
                if (confirmed) {
                    foreach (KeycodeV2 k in listHeldKeysMod)
                        rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                    listHeldKeysMod.Clear();

                    rc.SendAutotype(text);
                }
            } else {
#if DEBUG
                Console.WriteLine("Autotype blocked: too long or had a new line character");
#endif
            }
        }

        private void SwitchToLegacyRendering() {
            useMultiScreen = false;
            virtualRequireViewportUpdate = true;

            Dispatcher.Invoke((Action)delegate {
                toolScreenMode.Content = "Legacy";
                toolScreenOverview.Visibility = Visibility.Collapsed;
                toolZoomIn.Visibility = Visibility.Collapsed;
                toolZoomOut.Visibility = Visibility.Collapsed;
            });
        }

        private void SyncClipboard(object sender, EventArgs e) {
            try {
                if (ssClipboardSync) {
                    string temp = clipboard;
                    this.ToolClipboardSend_Click(sender, e);
                    if (clipboard != temp) {
                        toolClipboardSend.Background = System.Windows.Media.Brushes.Orange;
                        timerClipboard.Start();
                    }
#if DEBUG
                    Console.WriteLine("[Clipboard sync] Success?");
#endif
                }
            } catch (Exception) {
#if DEBUG
                Console.WriteLine("[Clipboard sync] Fail");
#endif
            }
        }

        private void ToolClipboardAutotype_Click(object sender, RoutedEventArgs e) {
            if (!controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            PerformAutotype();
        }

        private void ToolClipboardGet_Click(object sender, RoutedEventArgs e) {
            if (clipboard.Length > 0)
                Clipboard.SetDataObject(clipboard);
        }

        private void ToolClipboardSend_Click(object sender, EventArgs e) {
            if (state.connectionStatus != ConnectionStatus.Connected)
                return;

            clipboard = Clipboard.GetText();
            rc.SendClipboard(clipboard);

            //if (Settings.ClipboardSync > 0) toolClipboardGetText.Text = clipboard.Truncate(50);
        }

        private void ToolClipboardSend_MouseEnter(object sender, MouseEventArgs e) {
            clipboard = Clipboard.GetText();
            toolClipboardSendText.Text = clipboard.Truncate(50);
        }

        private void ToolClipboardSync_Click(object sender, RoutedEventArgs e) {
            ssClipboardSync = !ssClipboardSync;

            toolClipboardSync.Visibility = (ssClipboardSync ? Visibility.Visible : Visibility.Collapsed);
            toolClipboardReceiveOnly.Visibility = (!ssClipboardSync ? Visibility.Visible : Visibility.Collapsed);
        }

        private void ToolDisconnect_Click(object sender, RoutedEventArgs e) {
            //if (sessionId == null)
            //NotifySocketClosed(null);
            //else
            rc.Disconnect(state.sessionId);
        }

        private void ToolKeyWin_Click(object sender, RoutedEventArgs e) {
            KeyWinSet(!keyDownWin);
        }

        private void ToolMachineNoteLink_Click(object sender, RoutedEventArgs e) {
            string link = toolMachineNoteLink.Header.ToString();
            if (link.Contains("http"))
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        }

        private void ToolOptions_Click(object sender, RoutedEventArgs e) {
            Modules.RemoteControl.WindowOptions winOptions = new Modules.RemoteControl.WindowOptions(ref Settings) {
                Owner = this
            };
            winOptions.ShowDialog();
            LoadSettings();
            if (virtualViewWant != virtualCanvas) //Not in Overview?
                PositionCameraToCurrentScreen(); //Multi-Screen Alt Fit
        }

        private void ToolPanicRelease_Click(object sender, RoutedEventArgs e) {
            if (!controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.SendPanicKeyRelease();
            listHeldKeysMod.Clear();
            KeyWinSet(false);

            DebugKeyboard();
        }

        private void ToolReconnect_Click(object sender, RoutedEventArgs e) {
            //if (App.alternative != null && (socketAlive || App.alternative.socketActive))
            if (state.socketAlive && !requiredApproval)
                rc.Reconnect();
            else
                ToolShowAlternative_Click(sender, e);
        }

        private void ToolScreenOverview_Click(object sender, RoutedEventArgs e) {
            useMultiScreenOverview = !useMultiScreenOverview;
            if (useMultiScreenOverview) {
                SetControlEnabled(false);
                ChangeViewToOverview();
            } else {
                PositionCameraToCurrentScreen();
            }
        }

        private void ToolScreenshotToClipboard_Click(object sender, RoutedEventArgs e) {
            if (state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.CaptureNextScreen();
        }

        private void ToolSendCtrlAltDel_Click(object sender, RoutedEventArgs e) {
            if (!controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.SendSecureAttentionSequence();
        }

        private void ToolShowAlternative_Click(object sender, RoutedEventArgs e) {
            if (App.alternative == null)
                return;

            if (App.alternative.WindowState == System.Windows.WindowState.Minimized)
                App.alternative.WindowState = System.Windows.WindowState.Normal;
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

        private void ToolToggleControl_Click(object sender, RoutedEventArgs e) {
            SetControlEnabled(!controlEnabled);
        }

        private void ToolZoomIn_Click(object sender, RoutedEventArgs e) {
            if (!useMultiScreen)
                return;
            if (virtualViewWant.Width - 200 < 0 || virtualViewWant.Height - 200 < 0)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X + 100, virtualViewWant.Y + 100, virtualViewWant.Width - 200, virtualViewWant.Height - 200);
            virtualRequireViewportUpdate = true;

            //DebugKeyboard();
            glControl.Invalidate();
        }

        private void ToolZoomOut_Click(object sender, RoutedEventArgs e) {
            if (!useMultiScreen)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X - 100, virtualViewWant.Y - 100, virtualViewWant.Width + 200, virtualViewWant.Height + 200);
            virtualRequireViewportUpdate = true;

            //DebugKeyboard();
            glControl.Invalidate();
        }

        private void Window_Activated(object sender, EventArgs e) {
            if (!controlEnabled || state.CurrentScreen == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            KeyHookSet(true);
            state.windowActivatedMouseMove = true;
        }

        private void Window_Closed(object sender, EventArgs e) {
            if (shader_program != 0)
                GL.DeleteProgram(shader_program);
            if (fragment_shader_object != 0)
                GL.DeleteShader(fragment_shader_object);
            if (vertex_shader_object != 0)
                GL.DeleteShader(vertex_shader_object);

            if (App.alternative != null && App.alternative.Visibility != Visibility.Visible)
                Environment.Exit(0);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (keyHook.IsActive)
                keyHook.Uninstall();

            //NotifySocketClosed(sessionId);
            rc.Disconnect(state.sessionId);

            clipboardMon.OnUpdate -= SyncClipboard;
        }

        private void Window_Deactivated(object sender, EventArgs e) {
            KeyHookSet(true);
            if (!controlEnabled || state.CurrentScreen == null || state.connectionStatus != ConnectionStatus.Connected)
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

        private int progressValue;
        private Progress<int> progress;
        private ProgressDialog progressDialog;

        private void Window_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                if (progressDialog != null && progressDialog.IsBusy)
                    return;

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                bool doUpload = false;
                using (TaskDialog dialog = new TaskDialog()) {
                    dialog.WindowTitle = "KLC-Finch: Upload File";
                    dialog.MainInstruction = "Upload dropped file to c:\\temp?";
                    dialog.MainIcon = TaskDialogIcon.Information;
                    dialog.CenterParent = true;
                    dialog.Content = files[0];

                    TaskDialogButton tdbYes = new TaskDialogButton(ButtonType.Yes);
                    TaskDialogButton tdbCancel = new TaskDialogButton(ButtonType.Cancel);
                    dialog.Buttons.Add(tdbYes);
                    dialog.Buttons.Add(tdbCancel);

                    TaskDialogButton button = dialog.ShowDialog(this);
                    doUpload = (button == tdbYes);
                }

                if (doUpload) {
                    progressValue = 0;
                    progress = new Progress<int>(newValue => {
                        progressValue = newValue;
                    });
                    progressDialog = new ProgressDialog {
                        //ProgressBarStyle = ProgressBarStyle.MarqueeProgressBar,
                        WindowTitle = "KLC-Finch: Upload File",
                        Text = "Uploading to c:\\temp",
                        Description = "Source file: " + files[0],
                        ShowCancelButton = false,
                        ShowTimeRemaining = true
                    };
                    progressDialog.DoWork += ProgressDialog_DoWork;
                    progressDialog.Show();

                    rc.UploadDrop(files[0], progress);
                }
            }
        }

        private void ProgressDialog_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e) {
            while (progressValue < 100) {
                progressDialog.ReportProgress(progressValue);
                System.Threading.Thread.Sleep(100);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            winScreens.Owner = this;
            KeyHookSet(true);

            if (Settings.RendererAlt) {
                rc.DecodeMode = DecodeMode.BitmapRGB;
                this.Title = Title + " (RGB)"; 
            } else {
                rc.DecodeMode = DecodeMode.RawYUV;
                this.Title = Title + " (YUV)";
            }

            glVersion = GL.GetString(StringName.Version);

            CreateShaders(Shaders.yuvtorgb_vertex, Shaders.yuvtorgb_fragment, out vertex_shader_object, out fragment_shader_object, out shader_program);
            m_shader_sampler[0] = GL.GetUniformLocation(shader_program, "y_sampler");
            m_shader_sampler[1] = GL.GetUniformLocation(shader_program, "u_sampler");
            m_shader_sampler[2] = GL.GetUniformLocation(shader_program, "v_sampler");
            m_shader_multiplyColor = GL.GetUniformLocation(shader_program, "multiplyColor");
        }

        #region Host Desktop Configuration (Screens of current session)

        public override void UpdateScreenLayout(dynamic json, string jsonstr = "") {
            screenStatus = ScreenStatus.Preparing;

            ListScreen.Clear();
            previousScreen = state.CurrentScreen = null;

            string default_screen = json["default_screen"].ToString();
            state.connectionStatus = ConnectionStatus.Connected;

            Dispatcher.Invoke((Action)delegate {
                toolScreen.DropdownMenu.Items.Clear();

                foreach (dynamic screen in json["screens"]) {
                    string screen_id = screen["screen_id"].ToString(); //int or BigInteger
                    string screen_name = (string)screen["screen_name"];
                    int screen_height = (int)screen["screen_height"];
                    int screen_width = (int)screen["screen_width"];
                    int screen_x = (int)screen["screen_x"];
                    int screen_y = (int)screen["screen_y"];

                    //Add Screen
                    RCScreen newScreen = new RCScreen(screen_id, screen_name, screen_height, screen_width, screen_x, screen_y);
                    ListScreen.Add(newScreen);

                    //Add to toolbar menu
                    MenuItem item = new MenuItem {
                        Header = newScreen.ToString()// screen_name + ": (" + screen_width + " x " + screen_height + " at " + screen_x + ", " + screen_y + ")";
                    };
                    item.Click += new RoutedEventHandler(ToolScreen_ItemClicked);

                    toolScreen.DropdownMenu.Items.Add(item);

                    //Private and Mac seem to bug out if you change screens, cause there's only one screen
                    toolScreen.Opacity = (ListScreen.Count > 1 ? 1.0 : 0.6);

                    if (screen_id == default_screen) {
                        state.CurrentScreen = newScreen;
                        //Legacy
                        legacyVirtualHeight = state.CurrentScreen.rect.Height;
                        legacyVirtualWidth = state.CurrentScreen.rect.Width;
                        virtualRequireViewportUpdate = true;

                        toolScreen.Content = state.CurrentScreen.screen_name;
                        toolScreen.ToolTip = state.CurrentScreen.StringResPos();
                    }
                }
            });

            int lowestX = ListScreen.Min(x => x.rectFixed.X);
            int lowestY = ListScreen.Min(x => x.rectFixed.Y);
            int highestX = ListScreen.Max(x => x.rectFixed.Right);
            int highestY = ListScreen.Max(x => x.rectFixed.Bottom);
            SetCanvas(lowestX, lowestY, highestX, highestY);

            rc.UpdateScreens(jsonstr);
            winScreens.UpdateStartScreens(jsonstr);
            winScreens.SetCanvas(lowestX, lowestY, highestX, highestY);

            screenStatus = ScreenStatus.LayoutReady;
        }

        private void FromGlChangeScreen(RCScreen screen, bool moveCamera = true) {
            previousScreen = state.CurrentScreen;
            state.CurrentScreen = screen;
            rc.ChangeScreen(state.CurrentScreen.screen_id);

            if (useMultiScreen && moveCamera)
                PositionCameraToCurrentScreen();

            Dispatcher.Invoke((Action)delegate {
                toolScreen.Content = screen.screen_name;
                toolScreen.ToolTip = screen.StringResPos();

                foreach (MenuItem item in toolScreen.DropdownMenu.Items) {
                    item.IsChecked = (item.Header.ToString() == screen.ToString());
                }
            });
        }

        private void ToolScreen_ItemClicked(object sender, RoutedEventArgs e) {
            MenuItem source = (MenuItem)e.Source;
            string[] screen_selected = source.Header.ToString().Split(':');

            previousScreen = state.CurrentScreen;
            state.CurrentScreen = ListScreen.First(x => x.screen_name == screen_selected[0]);
            rc.ChangeScreen(state.CurrentScreen.screen_id);

            if (useMultiScreen)
                PositionCameraToCurrentScreen();

            toolScreen.Content = state.CurrentScreen.screen_name;
            toolScreen.ToolTip = state.CurrentScreen.StringResPos();
            foreach (MenuItem item in toolScreen.DropdownMenu.Items) {
                item.IsChecked = (item == source);
            }
        }

        public void SetScreen(string id) {
            previousScreen = state.CurrentScreen;
            state.CurrentScreen = ListScreen.First(x => x.screen_id == id);
            rc.ChangeScreen(state.CurrentScreen.screen_id);

            if (useMultiScreen)
                PositionCameraToCurrentScreen();

            Dispatcher.Invoke((Action)delegate {
                toolScreen.Content = state.CurrentScreen.screen_name;
                toolScreen.ToolTip = state.CurrentScreen.StringResPos();

                foreach (MenuItem item in toolScreen.DropdownMenu.Items) {
                    item.IsChecked = (item.Header.ToString() == state.CurrentScreen.ToString());
                }
            });
        }

        #endregion Host Desktop Configuration (Screens of current session)

        #region Host Terminal Sessions List

        public override void AddTSSession(string session_id, string session_name) {
            TSSession newTSSession = new TSSession(session_id, session_name);
            listTSSession.Add(newTSSession);

            Dispatcher.Invoke((Action)delegate {
                MenuItem item = new MenuItem {
                    Header = session_name
                };
                item.Click += new RoutedEventHandler(ToolTSSession_ItemClicked);

                toolTSSession.DropdownMenu.Items.Add(item);
                toolTSSession.Visibility = Visibility.Visible;

                if (currentTSSession == null) {
                    currentTSSession = newTSSession;
                    toolTSSession.Content = currentTSSession.session_name;
                }
            });
        }

        public override void ClearTSSessions() {
            listTSSession.Clear();
            currentTSSession = null;

            Dispatcher.Invoke((Action)delegate {
                toolTSSession.DropdownMenu.Items.Clear();
            });
        }

        private void ToolTSSession_ItemClicked(object sender, RoutedEventArgs e) {
            MenuItem source = (MenuItem)e.Source;
            source.IsChecked = true;
            string session_selected = source.Header.ToString();

            TSSession selectedTSSession = listTSSession.First(x => x.session_name == session_selected);
            if (currentTSSession == selectedTSSession)
                return; //There's a bug with being in legacy, selecting the same session, it changes back to multi-screen but the mouse is wrong.

            currentTSSession = selectedTSSession;
            rc.ChangeTSSession(currentTSSession.session_id);

            useMultiScreen = Settings.StartMultiScreen;

            toolTSSession.Content = currentTSSession.session_name;

            if (useMultiScreen) {
                toolScreenMode.Content = "Multi";
                toolScreenOverview.Visibility = Visibility.Visible;
                toolZoomIn.Visibility = Visibility.Visible;
                toolZoomOut.Visibility = Visibility.Visible;
            } else {
                toolScreenMode.Content = "Legacy";
                toolScreenOverview.Visibility = Visibility.Collapsed;
                toolZoomIn.Visibility = Visibility.Collapsed;
                toolZoomOut.Visibility = Visibility.Collapsed;
            }

            foreach (MenuItem item in toolTSSession.DropdownMenu.Items) {
                item.IsChecked = (item == source);
            }
        }

        #endregion Host Terminal Sessions List

        #region Mouse and Keyboard

        private readonly KeycodeV2 keyctrl = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.ControlKey);

        private readonly KeycodeV2 keyshift = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.ShiftKey);

        private readonly KeycodeV2 keywin = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.LWin);

        private readonly List<KeycodeV2> listHeldKeysMod = new List<KeycodeV2>();

        private readonly List<KeycodeV2> listHeldKeysOther = new List<KeycodeV2>();

        private void DebugKeyboard() {
            /*
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

            if (Settings.DisplayOverlayKeyboardMod || Settings.DisplayOverlayKeyboardOther) {
                Dispatcher.Invoke((Action)delegate {
                    txtDebugLeft.Text = strKeyboard;
                });
            }
            */
        }

        private void DebugMouseEvent(int X, int Y) {
            /*
            string strMousePos = "";

            if (Settings.DisplayOverlayMouse) {
                strMousePos = string.Format("X: {0}, Y: {1}", X, Y);
                Dispatcher.Invoke((Action)delegate {
                    txtDebugRight.Text = strMousePos;
                });
            }
            */

            /*string strMousePos = string.Format("X: {0}, Y: {1}", X, Y);

            if (useCanvasRGB) {
                Dispatcher.Invoke((Action)delegate {
                    txtDebugRight.Text = strMousePos;
                });
                return;
            }

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

            glControl.Invalidate();*/
        }

        private void ToolScreenMode_Click(object sender, RoutedEventArgs e) {
            useMultiScreen = !useMultiScreen;
            if (useMultiScreen) {
                useMultiScreenOverview = false;
                PositionCameraToCurrentScreen();

                toolScreenMode.Content = "Multi";
                toolScreenOverview.Visibility = Visibility.Visible;
                toolZoomIn.Visibility = Visibility.Visible;
                toolZoomOut.Visibility = Visibility.Visible;
            } else {
                toolScreenMode.Content = "Legacy";
                toolScreenOverview.Visibility = Visibility.Collapsed;
                toolZoomIn.Visibility = Visibility.Collapsed;
                toolZoomOut.Visibility = Visibility.Collapsed;
            }
        }

        private void ToolScreen_Click(object sender, RoutedEventArgs e) {
            TimeSpan span = DateTime.Now - winScreens.TimeDeactivated;
            if (Settings.ScreenSelectNew) {
                if (span.TotalMilliseconds < 500)
                    winScreens.Hide(); //Doesn't really do anything, will act same as Legacy
                else
                    winScreens.Show();
            }
            //Otherwise the old menu is shown
        }

        private void ToolClipboardPaste_Click(object sender, RoutedEventArgs e) {
            if (!controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            string text = Clipboard.GetText().Trim();
            rc.SendPasteClipboard(text);
        }

        private void ToolShowMouse_Click(object sender, RoutedEventArgs e) {
            toolShowMouse.IsChecked = !toolShowMouse.IsChecked;
            rc.ShowCursor(toolShowMouse.IsChecked);
        }

        private void ToolBlockScreen_Click(object sender, RoutedEventArgs e) {
            toolBlockScreen.IsChecked = !toolBlockScreen.IsChecked;
            rc.SendBlackScreenBlockInput(toolBlockScreen.IsChecked, toolBlockMouseKB.IsChecked);
        }

        private void ToolBlockMouseKB_Click(object sender, RoutedEventArgs e) {
            toolBlockMouseKB.IsChecked = !toolBlockMouseKB.IsChecked;
            rc.SendBlackScreenBlockInput(toolBlockScreen.IsChecked, toolBlockMouseKB.IsChecked);
        }

        public void UpdateScreenLayoutHack() {
            if (currentTSSession == null)
                rc.ChangeTSSession("0");
            else
                rc.ChangeTSSession(currentTSSession.session_id);
        }

        private void ToolUpdateScreenLayout_Click(object sender, RoutedEventArgs e) {
            UpdateScreenLayoutHack();
        }

        private void Window_StateChanged(object sender, EventArgs e) {
            /*
            if (WindowState == System.Windows.WindowState.Minimized) {
                if (glSupported && Settings.PowerSaveOnMinimize) {
                    powerSaving = true;
                    glControl.Render -= GlControl_Render;
                }
            } else {
                if (glSupported && powerSaving) {
                    powerSaving = false;
                    glControl.Render += GlControl_Render;
                }
            }
            */
        }

        private void toolOpenGLInfo_Click(object sender, RoutedEventArgs e) {
            MessageBox.Show("Render capability: 0x" + System.Windows.Media.RenderCapability.Tier.ToString("X") + "\r\n\r\nOpenGL Version: " + glVersion, "KLC-Finch: OpenGL Info");
        }

        private RCScreen GetScreenUsingMouse(int x, int y) {
            //This doesn't yet work in Canvas
            foreach (RCScreen screen in ListScreen) {
                if (screen.rect.Contains(x, y)) {
                    return screen;
                }
            }
            return null;
        }

        private void KeyWinSet(bool set) {
            if (!controlEnabled || endpointOS == Agent.OSProfile.Mac || state.connectionStatus != ConnectionStatus.Connected)
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

        private void Window_KeyUp2(System.Windows.Forms.KeyEventArgs e2) {
            if (e2.KeyCode == System.Windows.Forms.Keys.Scroll) {
                KeyHookSet(true);
                return;
            } else if (e2.KeyCode == System.Windows.Forms.Keys.PrintScreen) {
                rc.CaptureNextScreen();
            } else if (!controlEnabled) {
                if (e2.KeyCode == System.Windows.Forms.Keys.F2)
                    SetControlEnabled(true);
                return;
            }
            if (state.connectionStatus != ConnectionStatus.Connected)
                return;

            if (e2.KeyCode == System.Windows.Forms.Keys.Pause) {
                rc.SendPanicKeyRelease();
                listHeldKeysMod.Clear();
                KeyWinSet(false);
            } else {
                KeycodeV2 keykaseyaUN = KeycodeV2.ListUnhandled.Find(x => x.Key == e2.KeyCode);
                if (keykaseyaUN != null)
                    return;

                try {
                    KeycodeV2 keykaseya = KeycodeV2.List.Find(x => x.Key == e2.KeyCode);

                    if (endpointOS == Agent.OSProfile.Mac && Settings.MacSwapCtrlWin) {
                        if (KeycodeV2.ModifiersControl.Contains(e2.KeyCode))
                            keykaseya = keywin;
                        else if (e2.KeyCode == System.Windows.Forms.Keys.LWin)
                            keykaseya = keyctrl;
                    }

                    if (keykaseya == null)
                        throw new KeyNotFoundException(e2.KeyCode.ToString());

                    bool removed = (keykaseya.IsMod ? listHeldKeysMod.Remove(keykaseya) : listHeldKeysOther.Remove(keykaseya));

                    rc.SendKeyUp(keykaseya.JavascriptKeyCode, keykaseya.USBKeyCode);

                    if (keyHook.IsActive) {
                        if (keykaseya.Key == System.Windows.Forms.Keys.LWin || keykaseya.Key == System.Windows.Forms.Keys.RWin)
                            KeyWinSet(false);
                    } else {
                        if (keyDownWin && endpointOS != Agent.OSProfile.Mac) {
                            foreach (KeycodeV2 k in listHeldKeysOther)
                                rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                            listHeldKeysOther.Clear();
                            foreach (KeycodeV2 k in listHeldKeysMod)
                                rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                            listHeldKeysMod.Clear();

                            KeyWinSet(false);
                        }
                    }
                } catch {
#if DEBUG
                    Console.WriteLine("Up scan: " + e2.KeyCode + " / " + e2.KeyData + " / " + e2.KeyValue);
#endif
                }
            }

            DebugKeyboard();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e) {
            System.Windows.Forms.KeyEventArgs e2 = e.ToWinforms();
            Window_KeyUp2(e2);
            e.Handled = true;
        }

        private bool Window_PreviewKeyDown2(System.Windows.Forms.KeyEventArgs e2) {
            if (e2.KeyCode == System.Windows.Forms.Keys.F1) {
                SetControlEnabled(false);
                ChangeViewToOverview();
            }

            if (state.connectionStatus != ConnectionStatus.Connected || !controlEnabled)
                return false;

            if (e2.KeyCode == System.Windows.Forms.Keys.Pause || e2.KeyCode == System.Windows.Forms.Keys.Scroll) {
                //Done on release
                return true;
            } else if (e2.KeyCode == System.Windows.Forms.Keys.Oemtilde && e2.Control) {
                PerformAutotype();
            } else if (e2.KeyCode == System.Windows.Forms.Keys.V && e2.Control && e2.Shift) {
                PerformAutotype();
            } else {
                KeycodeV2 keykaseyaUN = KeycodeV2.ListUnhandled.Find(x => x.Key == e2.KeyCode);
                if (keykaseyaUN != null)
                    return false;

                try {
                    KeycodeV2 keykaseya = KeycodeV2.List.Find(x => x.Key == e2.KeyCode);

                    if (keykaseya == null)
                        throw new KeyNotFoundException(e2.KeyCode.ToString());

                    if (endpointOS == Agent.OSProfile.Mac && Settings.MacSwapCtrlWin) {
                        if (KeycodeV2.ModifiersControl.Contains(e2.KeyCode))
                            keykaseya = keywin;
                        else if (e2.KeyCode == System.Windows.Forms.Keys.LWin)
                            keykaseya = keyctrl;
                    }

                    if (keykaseya.Key == System.Windows.Forms.Keys.LWin || keykaseya.Key == System.Windows.Forms.Keys.RWin)
                        KeyWinSet(true);

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
#if DEBUG
                    Console.WriteLine("DOWN scan: " + e2.KeyCode + " / " + e2.KeyData + " / " + e2.KeyValue);
#endif
                }

                DebugKeyboard();
            }

            return true;
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            //Apparently preview is used because arrow keys?

            System.Windows.Forms.KeyEventArgs e2 = e.ToWinforms();
            Window_PreviewKeyDown2(e2);
            e.Handled = true;
        }

        #endregion Mouse and Keyboard

        /* Unused: Camera movement with keyboard
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
    }
}