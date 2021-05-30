﻿using KLC;
using LibKaseya;
using NTR;
using nucs.JsonSettings;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        private bool useMultiScreen = true;
        private bool virtualRequireViewportUpdate = false;
        private bool controlEnabled = false;

        private string clipboard = "";
        private KLC.ClipBoardMonitor clipboardMon;
        private bool socketAlive = false;
        //private bool reachedFirstConnect = false;
        //--
        private List<TSSession> listTSSession = new List<TSSession>();
        private TSSession currentTSSession = null;
        //--
        private List<RCScreen> listScreen = new List<RCScreen>();
        private RCScreen currentScreen = null;
        private RCScreen previousScreen = null;
        private TextureCursor textureCursor = null;

        private bool isMac;
        private string sessionId;

        private RemoteControl rc;

        private Rectangle virtualCanvas, virtualViewWant, virtualViewNeed;
        int legacyVirtualWidth, legacyVirtualHeight;
        int vpX, vpY;
        double scaleX, scaleY;

        private Camera MainCamera;

        private static Font arial = new Font("Arial", 32);
        Bitmap overlay2dMouse;
        Bitmap overlay2dKeyboard;
        Bitmap overlay2dDisconnected;
        Bitmap overlay2dControlOff;
        byte[] textureOverlayDataMouse;
        byte[] textureOverlayDataKeyboard;
        byte[] textureOverlayDataDisconnected;
        byte[] textureOverlayDataControlOff;
        bool overlayNewMouse;
        bool overlayNewKeyboard;
        int textureOverlay2dMouse;
        int textureOverlay2dKeyboard;
        int textureOverlay2dDisconnected;
        int textureOverlay2dControlOff;
        private const int overlayWidth = 400;
        private const int overlayHeight = 100;

        Vector2[] vertBufferScreen;
        Vector2[] vertBufferMouse, vertBufferKeyboard, vertBufferTop, vertBufferCenter;
        int VBOScreen;
        int VBOmouse, VBOkeyboard, VBOtop, VBOcenter;
        private TextureScreen textureLegacy;

        #region YUV
        //YUV source: https://github.com/minghuam/opentk-gst/blob/master/ThreadedGLSLVideoPlayer.cs
        object lockFrameBuf = new object();
        int vertex_shader_object = 0;
        int fragment_shader_object = 0;
        int shader_program = 0;
        int[] m_shader_sampler = new int[3];
        int m_shader_multiplyColor = 0;
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

        //--

        public WindowViewerV2(RemoteControl rc, int virtualWidth = 1920, int virtualHeight = 1080, bool isMac = false) {
            InitializeComponent();
            toolVersion.Header = "Build date: " + App.Version;

            this.isMac = isMac;

            string pathSettings = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\KLC-Finch-config.json";
            if (File.Exists(pathSettings))
                Settings = JsonSettings.Load<Settings>(pathSettings);
            else
                Settings = JsonSettings.Construct<Settings>(pathSettings);

            this.Width = Settings.RemoteControlWidth;
            this.Height = Settings.RemoteControlHeight;

            toolSettingStartControlEnabled.IsChecked = Settings.StartControlEnabled;
            toolSettingMacSwapCtrlWin.IsChecked = Settings.MacSwapCtrlWin;
            toolSettingMultiAltFit.IsChecked = Settings.MultiAltFit;
            toolSettingUseYUVShader.IsChecked = Settings.UseYUVShader;
            toolDebugKeyboardMod.IsChecked = Settings.DisplayOverlayKeyboardMod;
            toolDebugKeyboardOther.IsChecked = Settings.DisplayOverlayKeyboardOther;
            toolDebugMouse.IsChecked = Settings.DisplayOverlayMouse;
            // This repetition needs to be fixed
            toolClipboardSync.Visibility = (Settings.ClipboardSyncEnabled ? Visibility.Visible : Visibility.Collapsed);
            toolClipboardReceiveOnly.Visibility = (!Settings.ClipboardSyncEnabled ? Visibility.Visible : Visibility.Collapsed);

            if (isMac && Settings.MacSwapCtrlWin)
                toolKeyWin.Visibility = Visibility.Collapsed;

            this.rc = rc;
            socketAlive = false;
            connectionStatus = ConnectionStatus.FirstConnectionAttempt;
            SetControlEnabled(false, false); //Just for the visual

            //Multi-screen
            MainCamera = new Camera(Vector2.Zero);
            //Legacy
            if(!useMultiScreen)
                SetVirtual(0, 0, virtualWidth, virtualHeight);

            clipboardMon = new ClipBoardMonitor();
            clipboardMon.OnUpdate += SyncClipboard;
        }

        void CreateShaders(string vs, string fs, out int vertexObject, out int fragmentObject, out int program) {
            int status_code;
            string info;

            vertexObject = GL.CreateShader(ShaderType.VertexShader);
            fragmentObject = GL.CreateShader(ShaderType.FragmentShader);

            // Compile vertex shader
            GL.ShaderSource(vertexObject, vs);
            GL.CompileShader(vertexObject);
            GL.GetShaderInfoLog(vertexObject, out info);
            GL.GetShader(vertexObject, ShaderParameter.CompileStatus, out status_code);

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

        private void Window_Loaded(object sender, RoutedEventArgs e) {
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (shader_program != 0)
                GL.DeleteProgram(shader_program);
            if (fragment_shader_object != 0)
                GL.DeleteShader(fragment_shader_object);
            if (vertex_shader_object != 0)
                GL.DeleteShader(vertex_shader_object);

            clipboardMon.OnUpdate -= SyncClipboard;

            if (rc != null)
                rc.Disconnect(sessionId);

            if (App.alternative != null && App.alternative.Visibility != Visibility.Visible)
                Environment.Exit(0);
        }

        public void SetTitle(string title) {
            this.Title = title;
        }

        public void SetApprovalAndSpecialNote(int rcNotify, int machineShowToolTip, string machineNote, string machineNoteLink) {
            if (rcNotify < 3)
                txtRcNotify.Visibility = Visibility.Collapsed;

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

        public void ClearApproval() {
            Dispatcher.Invoke((Action)delegate {
                txtRcNotify.Visibility = Visibility.Collapsed;
            });
        }

        public void SetSessionID(string sessionId) {
            this.sessionId = sessionId;

            if (sessionId != null) {
                socketAlive = true;
                //connectionStatus = ConnectionStatus.Connected;
            }
        }

        public void SetCanvas(int virtualX, int virtualY, int virtualWidth, int virtualHeight)
        {
            if (useMultiScreen) {
                virtualCanvas = new Rectangle(virtualX, virtualY, Math.Abs(virtualX) + virtualWidth, Math.Abs(virtualY) + virtualHeight);
                SetVirtual(virtualX, virtualY, virtualCanvas.Width, virtualCanvas.Height);
            } else {
                virtualCanvas = new Rectangle(0, 0, virtualWidth, virtualHeight);
                SetVirtual(0, 0, virtualWidth, virtualHeight);
            }
        }

        public void SetVirtual(int virtualX, int virtualY, int virtualWidth, int virtualHeight)
        {
            if(useMultiScreen) {
                virtualViewWant = new Rectangle(virtualX, virtualY, virtualWidth, virtualHeight);
            } else {
                this.legacyVirtualWidth = virtualWidth;
                this.legacyVirtualHeight = virtualHeight;
                virtualCanvas = virtualViewWant = new Rectangle(0, 0, virtualWidth, virtualHeight);
            }

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

        public void NotifySocketClosed(string sessionId) {
            if (sessionId == "/control/agent") {
            } else if (this.sessionId != sessionId)
                return;

            socketAlive = false;
            connectionStatus = ConnectionStatus.Disconnected;

            rc = null;
            Dispatcher.Invoke((Action)delegate {
                toolLatency.Content = "N/C";
                txtRcNotify.Visibility = Visibility.Collapsed;
            });

            glControl.Invalidate();
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
                foreach (RCScreen screen in listScreen) {
                    if (screen.Texture == null)
                        screen.Texture = new TextureScreen((rc != null ? Settings.UseYUVShader : false));
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

            switch (connectionStatus) {
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
                foreach (RCScreen screen in listScreen) {
                    if (screen.Texture != null) {

                        Color multiplyColor;
                        if (screen == currentScreen)
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

                            GL.Vertex2(screen.rect.Left, screen.rect.Bottom);
                            GL.Vertex2(screen.rect.Left, screen.rect.Top);
                            GL.Vertex2(screen.rect.Right, screen.rect.Top);
                            GL.Vertex2(screen.rect.Right, screen.rect.Bottom);

                            //GL.Vertex2(vertBufferScreen[0].X, vertBufferScreen[0].Y);

                            GL.End();
                        }
                    }
                }

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

            if (connectionStatus == ConnectionStatus.Disconnected) {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dDisconnected);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOcenter);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferCenter.Length / 2);
            } else if (connectionStatus == ConnectionStatus.Connected && !controlEnabled) {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dControlOff);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOtop);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferTop.Length / 2);
            }
            #endregion

            //--

            glControl.SwapBuffers();
        }

        public void LoadCursor(int cursorX, int cursorY, int cursorWidth, int cursorHeight, int cursorHotspotX, int cursorHotspotY, byte[] remaining) {
            if (textureCursor != null)
                textureCursor.Load(new Rectangle(cursorX, cursorY, cursorWidth, cursorHeight), remaining);
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
            #endregion

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
            #endregion

            textureLegacy = new TextureScreen((rc != null ? Settings.UseYUVShader : false));
            //InitLegacyScreenTexture();

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.Enable(EnableCap.Texture2D);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        public void UpdateLatency(long ms) {
            Dispatcher.Invoke((Action)delegate {
                toolLatency.Content = string.Format("{0} ms", ms);
            });
        }

        #region Host Desktop Configuration (Screens of current session)
        public void ClearScreens() {
            listScreen.Clear();
            previousScreen = currentScreen = null;

            Dispatcher.Invoke((Action)delegate {
                toolScreen.DropdownMenu.Items.Clear();
            });

            connectionStatus = ConnectionStatus.Connected;
        }

        public void AddScreen(string screen_id, string screen_name, int screen_height, int screen_width, int screen_x, int screen_y, bool isDefault) {
            RCScreen newScreen = new RCScreen(screen_id, screen_name, screen_height, screen_width, screen_x, screen_y);
            listScreen.Add(newScreen);
            if (currentScreen == null) {
                currentScreen = newScreen;

                //Legacy
                legacyVirtualHeight = currentScreen.rect.Height;
                legacyVirtualWidth = currentScreen.rect.Width;
                virtualRequireViewportUpdate = true;
            }
            

            Dispatcher.Invoke((Action)delegate {
                MenuItem item = new MenuItem();
                item.Header = newScreen.ToString();// screen_name + ": (" + screen_width + " x " + screen_height + " at " + screen_x + ", " + screen_y + ")";
                item.Click += new RoutedEventHandler(toolScreen_ItemClicked);

                toolScreen.DropdownMenu.Items.Add(item);

                //Private and Mac seem to bug out if you change screens, cause there's only one screen
                toolScreen.IsEnabled = (listScreen.Count > 1);

                if (isDefault) {
                    toolScreen.Content = currentScreen.screen_name;
                    toolScreen.ToolTip = currentScreen.StringResPos();
                }
            });
        }

        private void toolScreen_ItemClicked(object sender, RoutedEventArgs e) {
            MenuItem source = (MenuItem)e.Source;
            string[] screen_selected = source.Header.ToString().Split(':');

            previousScreen = currentScreen;
            currentScreen = listScreen.First(x => x.screen_name == screen_selected[0]);
            rc.ChangeScreen(currentScreen.screen_id);

            if (useMultiScreen)
                PositionCameraToCurrentScreen();

            toolScreen.Content = currentScreen.screen_name;
            toolScreen.ToolTip = currentScreen.StringResPos();
            foreach (MenuItem item in toolScreen.DropdownMenu.Items) {
                item.IsChecked = (item == source);
            }
        }

        private void FromGlChangeScreen(RCScreen screen, bool moveCamera=true) {
            previousScreen = currentScreen;
            currentScreen = screen;
            rc.ChangeScreen(currentScreen.screen_id);

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
        #endregion

        #region Host Terminal Sessions List
        //public void SetActiveTSSession(string session_id) {
            //currentTSSession = session_id;
        //}

        public void AddTSSession(string session_id, string session_name) {
            TSSession newTSSession = new TSSession(session_id, session_name);
            listTSSession.Add(newTSSession);

            Dispatcher.Invoke((Action)delegate {
                MenuItem item = new MenuItem();
                item.Header = session_name;
                item.Click += new RoutedEventHandler(toolTSSession_ItemClicked);

                toolTSSession.DropdownMenu.Items.Add(item);
                toolTSSession.Visibility = Visibility.Visible;

                if (currentTSSession == null) {
                    currentTSSession = newTSSession;
                    toolTSSession.Content = currentTSSession.session_name;
                }
            });
        }

        public void ClearTSSessions() {
            listTSSession.Clear();
            currentTSSession = null;

            Dispatcher.Invoke((Action)delegate {
                toolTSSession.DropdownMenu.Items.Clear();
            });
        }

        private void toolTSSession_ItemClicked(object sender, RoutedEventArgs e) {
            MenuItem source = (MenuItem)e.Source;
            source.IsChecked = true;
            string session_selected = source.Header.ToString();

            TSSession selectedTSSession = listTSSession.First(x => x.session_name == session_selected);
            if (currentTSSession == selectedTSSession)
                return; //There's a bug with being in legacy, selecting the same session, it changes back to multi-screen but the mouse is wrong.

            currentTSSession = selectedTSSession;
            rc.ChangeTSSession(currentTSSession.session_id);

            useMultiScreen = true;
            
            //Dispatcher.Invoke((Action)delegate {
                toolTSSession.Content = currentTSSession.session_name;
                toolScreenOverview.Content = "Overview";
                //toolScreenOverview.IsEnabled = true;
                toolZoomIn.Visibility = Visibility.Visible;
                toolZoomOut.Visibility = Visibility.Visible;
            //});

            foreach (MenuItem item in toolTSSession.DropdownMenu.Items) {
                item.IsChecked = (item == source);
            }
        }
        #endregion

        private void PositionCameraToCurrentScreen() {
            if (!useMultiScreen)
                return;

            //MainCamera.Rotation = 0f;
            MainCamera.Position = Vector2.Zero;
            MainCamera.Scale = new Vector2(1f, 1f);
            DebugKeyboard();

            if (Settings.MultiAltFit) {
                bool adjustLeft = false;
                bool adjustUp = false;
                bool adjustRight = false;
                bool adjustDown = false;

                foreach (RCScreen screen in listScreen) {
                    if (screen == currentScreen)
                        continue;

                    if (screen.rect.Right <= currentScreen.rect.Left)
                        adjustLeft = true;
                    if (screen.rect.Bottom <= currentScreen.rect.Top)
                        adjustUp = true;
                    if (screen.rect.Left >= currentScreen.rect.Right)
                        adjustRight = true;
                    if (screen.rect.Top >= currentScreen.rect.Bottom)
                        adjustDown = true;
                }

                SetVirtual(currentScreen.rect.X - (adjustLeft ? 80 : 0),
                    currentScreen.rect.Y - (adjustUp ? 80 : 0),
                    currentScreen.rect.Width + (adjustLeft ? 80 : 0) + (adjustRight ? 80 : 0),
                    currentScreen.rect.Height + (adjustUp ? 80 : 0) + (adjustDown ? 80 : 0));
            } else
                SetVirtual(currentScreen.rect.X, currentScreen.rect.Y, currentScreen.rect.Width, currentScreen.rect.Height);
            glControl.Invalidate();
        }

        private void toolScreenOverview_Click(object sender, RoutedEventArgs e)
        {
            if (!useMultiScreen) {
                useMultiScreen = true;
                Dispatcher.Invoke((Action)delegate {
                    toolScreenOverview.Content = "Overview";
                    //toolScreenOverview.IsEnabled = true;
                    toolZoomIn.Visibility = Visibility.Visible;
                    toolZoomOut.Visibility = Visibility.Visible;
                });
                //return;
            }

            SetControlEnabled(false);
            ChangeViewToOverview();
        }

        private void toolSwitchToLegacy_Click(object sender, RoutedEventArgs e) {
            SwitchToLegacyRendering();
        }

        private void SwitchToLegacyRendering() {
            useMultiScreen = false;

            Dispatcher.Invoke((Action)delegate {
                toolScreenOverview.Content = "Legacy";
                //toolScreenOverview.IsEnabled = false;
                toolZoomIn.Visibility = Visibility.Collapsed;
                toolZoomOut.Visibility = Visibility.Collapsed;
            });
        }

        private void ChangeViewToOverview() {
            if (!useMultiScreen)
                return;

            int lowestX = 0;
            int lowestY = 0;
            int highestX = 0;
            int highestY = 0;
            foreach(RCScreen screen in listScreen) {
                lowestX = Math.Min(screen.rect.X, lowestX);
                lowestY = Math.Min(screen.rect.Y, lowestY);
                highestX = Math.Max(screen.rect.X + screen.rect.Width, highestX);
                highestY = Math.Max(screen.rect.Y + screen.rect.Height, highestY);
            }
            SetCanvas(lowestX, lowestY, highestX, highestY);

            //--

            //MainCamera.Rotation = 0f;
            MainCamera.Position = Vector2.Zero;
            MainCamera.Scale = new Vector2(1f, 1f);
            DebugKeyboard();

            virtualViewWant = virtualCanvas;
            virtualRequireViewportUpdate = true;
            glControl.Invalidate();
        }

        public void LoadTexture(int width, int height, Bitmap decomp) {
            //lock (lockFrameBuf) {
                if (useMultiScreen) {
                    #region Multi-Screen
                    RCScreen scr = null;
                    if (currentScreen != null && currentScreen.rect.Width == width && currentScreen.rect.Height == height)
                        scr = currentScreen;
                    else if (previousScreen != null && previousScreen.rect.Width == width && previousScreen.rect.Height == height)
                        scr = previousScreen;
                    else {
                        List<RCScreen> scrMatch = listScreen.FindAll(x => x.rect.Width == width && x.rect.Height == height);
                        if (scrMatch.Count == 1) {
                            scr = scrMatch[0];
                        } else {
                            Console.WriteLine("Forced switch from Multi-Screen to Legacy");
                            LoadTexture(width, height, decomp);
                            SwitchToLegacyRendering();
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
                    socketAlive = true;
                    #endregion
                } else {
                    #region Legacy
                    if (currentScreen == null)
                        return;

                    if (legacyVirtualWidth != width || legacyVirtualHeight != height) {
                        Console.WriteLine("[LoadTexture:Legacy] Virtual resolution did not match texture received.");
                        SetVirtual(0, 0, width, height);

                        try {
                            currentScreen.rect.Width = width;
                            currentScreen.rect.Height = height;
                            //This is a sad attempt a fixing a problem when changing left monitor's size.
                            //However if changing a middle monitor, the right monitor will break.
                            //The reconnect button can otherwise be used, or perhaps a multimonitor/scan feature can be added to automatically detect and repair the list of screens.
                            if (currentScreen.rect.X < 0)
                                currentScreen.rect.X = width * -1;
                        } catch (Exception ex) {
                            Console.WriteLine("[LoadTexture:Legacy] " + ex.ToString());
                        }
                    }

                    textureLegacy.Load(new Rectangle(0, 0, width, height), decomp);

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
                    socketAlive = true;
                    #endregion
                }
            //}

            glControl.Invalidate();
        }

        public void LoadTextureRaw(byte[] buffer, int width, int height, int stride) {
            if (width * height <= 0)
                return;

            //lock (lockFrameBuf) {
                if (useMultiScreen) {
                    #region Multi-Screen
                    RCScreen scr = null;
                    if (currentScreen != null && currentScreen.rect.Width == width && currentScreen.rect.Height == height)
                        scr = currentScreen;
                    else if (previousScreen != null && previousScreen.rect.Width == width && previousScreen.rect.Height == height)
                        scr = previousScreen;
                    else {
                        List<RCScreen> scrMatch = listScreen.FindAll(x => x.rect.Width == width && x.rect.Height == height);
                        if (scrMatch.Count == 1) {
                            scr = scrMatch[0];
                        } else {
                            Console.WriteLine("Forced switch from Multi-Screen to Legacy");
                            LoadTextureRaw(buffer, width, height, stride);
                            SwitchToLegacyRendering();
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
                    socketAlive = true;
                    #endregion
                } else {
                    #region Legacy
                    if (currentScreen == null)
                        return;

                    if (legacyVirtualWidth != width || legacyVirtualHeight != height) {
                        Console.WriteLine("[LoadTexture:Legacy] Virtual resolution did not match texture received.");
                        SetVirtual(0, 0, width, height);

                        try {
                            currentScreen.rect.Width = width;
                            currentScreen.rect.Height = height;
                            //This is a sad attempt a fixing a problem when changing left monitor's size.
                            //However if changing a middle monitor, the right monitor will break.
                            //The reconnect button can otherwise be used, or perhaps a multimonitor/scan feature can be added to automatically detect and repair the list of screens.
                            if (currentScreen.rect.X < 0)
                                currentScreen.rect.X = width * -1;
                        } catch (Exception ex) {
                            Console.WriteLine("[LoadTexture:Legacy] " + ex.ToString());
                        }
                    }

                    textureLegacy.LoadRaw(new Rectangle(0, 0, width, height), buffer, stride);

                    socketAlive = true;
                    #endregion
                }
            //}

            glControl.Invalidate();
        }

        /*
        void UpdateTexture(int w, int h, int stride) {
            //GL.Enable(EnableCap.Texture2D);
            GL.PixelStore(PixelStoreParameter.UnpackRowLength, stride);

            //Y
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, textureIDs[0]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.One, w, h, 0, OpenTK.Graphics.OpenGL.PixelFormat.Luminance, PixelType.UnsignedByte, textureLegacyData);

            GL.PixelStore(PixelStoreParameter.UnpackRowLength, stride / 2);

            //U
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, textureIDs[1]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.One, w / 2, h / 2, 0, OpenTK.Graphics.OpenGL.PixelFormat.Luminance, PixelType.UnsignedByte, textureLegacyDataU);

            //V
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, textureIDs[2]);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.One, w / 2, h / 2, 0, OpenTK.Graphics.OpenGL.PixelFormat.Luminance, PixelType.UnsignedByte, textureLegacyDataV);

            GL.PixelStore(PixelStoreParameter.UnpackRowLength, 0); //Reset stride
            //GL.Disable(EnableCap.Texture2D);

            GL.ActiveTexture(TextureUnit.Texture0);
        }
        */

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

            toolClipboardSync.Visibility = (Settings.ClipboardSyncEnabled ? Visibility.Visible : Visibility.Collapsed);
            toolClipboardReceiveOnly.Visibility = (!Settings.ClipboardSyncEnabled ? Visibility.Visible : Visibility.Collapsed);
        }

        private void toolClipboardSend_MouseEnter(object sender, MouseEventArgs e) {
            clipboard = Clipboard.GetText();
            Dispatcher.Invoke((Action)delegate {
                toolClipboardSendText.Text = clipboard.Truncate(50); //.Replace("\r", "").Replace("\n", "").Truncate(50);
            });
        }

        private void toolClipboardSend_Click(object sender, EventArgs e) {
            if (rc == null)
                return;

            clipboard = Clipboard.GetText();
            Dispatcher.Invoke((Action)delegate {
                //toolClipboardSend.ToolTip = "Send to Client: " + clipboard.Replace("\r", "").Replace("\n", "").Truncate(50);
                rc.SendClipboard(clipboard);

                if (Settings.ClipboardSyncEnabled)
                    toolClipboardGetText.Text = clipboard.Truncate(50); //.Replace("\r", "").Replace("\n", "").Truncate(50);
            });
        }

        public void ReceiveClipboard(string content) {
            if (clipboard == content)
                return;

            clipboard = content;
            Dispatcher.Invoke((Action)delegate {
                //try {
                toolClipboardGetText.Text = clipboard.Truncate(50); //.Replace("\r", "").Replace("\n", "").Truncate(50);

                //if (clipboardSyncEnabled) { //Commented out now that we use Receive-Only mode
                //this.BeginInvoke(new Action(() => {
                if (clipboard.Length > 0)
                    Clipboard.SetDataObject(clipboard);
                //Clipboard.SetText(clipboard); //Apparently WPF clipboard has issues
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
            if (isStart) {
                if(Settings.StartControlEnabled) {
                    controlEnabled = value;
                    PositionCameraToCurrentScreen();
                }
            } else
                controlEnabled = value;

            Dispatcher.Invoke((Action)delegate {
                if (controlEnabled)
                    toolToggleControl.Content = "Control Enabled";
                else
                    toolToggleControl.Content = "Control Disabled";
                toolToggleControl.FontWeight = (controlEnabled ? FontWeights.Normal : FontWeights.Bold);

                toolSendCtrlAltDel.IsEnabled = controlEnabled;

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
            else
                toolShowAlternative_Click(sender, e);
        }

        KeycodeV2 keyctrl = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.ControlKey);
        KeycodeV2 keyshift = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.ShiftKey);
        KeycodeV2 keywin = KeycodeV2.List.Find(x => x.Key == System.Windows.Forms.Keys.LWin);

        private void HandleMouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (rc == null)
                return;

            if (useMultiScreen) {
                Vector2 point = MainCamera.ScreenToWorldCoordinates(new Vector2((float)(e.X / scaleX), (float)(e.Y / scaleY)), virtualViewNeed.X, virtualViewNeed.Y);
                RCScreen screenPointingTo = GetScreenUsingMouse(point);
                if (screenPointingTo == null)
                    return;

                if (controlEnabled) {
                    if (virtualViewWant != virtualCanvas && screenPointingTo != currentScreen) {
                        FromGlChangeScreen(screenPointingTo, true);
                        return;
                    }
                } else {
                    if (e.Clicks == 2) {
                        SetControlEnabled(true);
                    } else if (e.Button == System.Windows.Forms.MouseButtons.Left) {
                        if (currentScreen != screenPointingTo) //Multi-Screen (Focused), Control Disabled, Change Screen
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
                PerformAutotype();
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
                Console.WriteLine("Autotype blocked: too long or had a new line character");
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
            if (!useMultiScreen)
                return;
            if (virtualViewWant.Width - 200 < 0 || virtualViewWant.Height - 200 < 0)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X + 100, virtualViewWant.Y + 100, virtualViewWant.Width - 200, virtualViewWant.Height - 200);
            virtualRequireViewportUpdate = true;

            DebugKeyboard();
            glControl.Invalidate();
        }

        private void toolZoomOut_Click(object sender, RoutedEventArgs e) {
            if (!useMultiScreen)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X - 100, virtualViewWant.Y - 100, virtualViewWant.Width + 200, virtualViewWant.Height + 200);
            virtualRequireViewportUpdate = true;

            DebugKeyboard();
            glControl.Invalidate();
        }

        private void glControl_Load(object sender, EventArgs e) {
            //check GLSL
            string version = GL.GetString(StringName.Version);
            int major = (int)version[0];
            if (major < 2)
                Console.WriteLine("OpenGL 2.0 not available. GLSL not supported.");
            else if(rc != null) {
                this.rc.UseYUVShader = Settings.UseYUVShader;
                if (Settings.UseYUVShader)
                    this.Title = Title + " (YUV)";
                else
                    this.Title = Title + " (RGB)";

                CreateShaders(Shaders.yuvtorgb_vertex, Shaders.yuvtorgb_fragment, out vertex_shader_object, out fragment_shader_object, out shader_program);
                m_shader_sampler[0] = GL.GetUniformLocation(shader_program, "y_sampler");
                m_shader_sampler[1] = GL.GetUniformLocation(shader_program, "u_sampler");
                m_shader_sampler[2] = GL.GetUniformLocation(shader_program, "v_sampler");
                m_shader_multiplyColor = GL.GetUniformLocation(shader_program, "multiplyColor");
            }

            //--

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

        private void toolSettingMacSwapCtrlWin_Click(object sender, RoutedEventArgs e) {
            toolSettingMacSwapCtrlWin.IsChecked = Settings.MacSwapCtrlWin = !Settings.MacSwapCtrlWin;
        }

        private void toolSettingMultiAltFit_Click(object sender, RoutedEventArgs e) {
            toolSettingMultiAltFit.IsChecked = Settings.MultiAltFit = !Settings.MultiAltFit;

            PositionCameraToCurrentScreen();
        }

        private void toolOptions_Click(object sender, RoutedEventArgs e) {
            new Modules.RemoteControl.WindowOptions().ShowDialog();
        }

        private void toolMachineNoteLink_Click(object sender, RoutedEventArgs e) {
            string link = toolMachineNoteLink.Header.ToString();
            if(link.Contains("http"))
                Process.Start(new ProcessStartInfo(link) { UseShellExecute = true });
        }

        private void toolClipboardAutotype_Click(object sender, RoutedEventArgs e) {
            if (rc == null || !controlEnabled)
                return;

            PerformAutotype();
        }

        private void toolSettingUseYUVShader_Click(object sender, RoutedEventArgs e) {
            if (rc != null) {
                Settings.UseYUVShader = !Settings.UseYUVShader;
                Settings.Save();
                rc.Reconnect();
            }
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
            if (currentScreen == null || rc == null)
                return;

            windowActivatedMouseMove = false;

            if (useMultiScreen) {
                Vector2 point = MainCamera.ScreenToWorldCoordinates(new Vector2((float)(e.X / scaleX), (float)(e.Y / scaleY)), virtualViewNeed.X, virtualViewNeed.Y);
                DebugMouseEvent((int)point.X, (int)point.Y);

                RCScreen screenPointingTo = GetScreenUsingMouse(point);
                if (screenPointingTo == null)
                    return;

                if (virtualViewWant == virtualCanvas && currentScreen.screen_id != screenPointingTo.screen_id) {
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

                legacyPoint.X = legacyPoint.X + currentScreen.rect.X;
                legacyPoint.Y = legacyPoint.Y + currentScreen.rect.Y;

                DebugMouseEvent(legacyPoint.X, legacyPoint.Y);

                rc.SendMousePosition(legacyPoint.X, legacyPoint.Y);
            }
        }

        private RCScreen GetScreenUsingMouse(Vector2 point) {
            foreach (RCScreen screen in listScreen) {
                if (screen.rect.Contains((int)point.X, (int)point.Y)) {
                    return screen;
                }
            }
            return null;
        }

        private void HandleMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!controlEnabled || rc == null)
                return;

            rc.SendMouseWheel(e.Delta);
        }

        private void OpenTkControl_PreviewKeyDown(object sender, KeyEventArgs e) {
            //Apparently preview is used because arrow keys?
            if (rc == null)
                return;

            System.Windows.Forms.KeyEventArgs e2 = e.ToWinforms();
            if (e2.KeyCode == System.Windows.Forms.Keys.F1) {
                SetControlEnabled(false);
                ChangeViewToOverview();
            }

            if (!controlEnabled)
                return;

            if (e2.KeyCode == System.Windows.Forms.Keys.Pause) {
                //Done on release
                e.Handled = true;
                return;
            } else if(e2.KeyCode == System.Windows.Forms.Keys.Oemtilde && e2.Control) {
                PerformAutotype();
            } else if (e2.KeyCode == System.Windows.Forms.Keys.V && e2.Control && e2.Shift) {
                PerformAutotype();
            } else {
                KeycodeV2 keykaseyaUN = KeycodeV2.ListUnhandled.Find(x => x.Key == e2.KeyCode);
                if (keykaseyaUN != null)
                    return;

                try {
                    KeycodeV2 keykaseya = KeycodeV2.List.Find(x => x.Key == e2.KeyCode);

                    if (keykaseya == null)
                        throw new KeyNotFoundException(e2.KeyCode.ToString());

                    if (isMac && Settings.MacSwapCtrlWin) {
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
                if (e2.KeyCode == System.Windows.Forms.Keys.F2)
                    SetControlEnabled(true);
            } else if (e2.KeyCode == System.Windows.Forms.Keys.Pause) {
                rc.SendPanicKeyRelease();
                listHeldKeysMod.Clear();
                KeyWinSet(false);
                DebugKeyboard();
            } else {
                KeycodeV2 keykaseyaUN = KeycodeV2.ListUnhandled.Find(x => x.Key == e2.KeyCode);
                if (keykaseyaUN != null)
                    return;

                try {
                    KeycodeV2 keykaseya = KeycodeV2.List.Find(x => x.Key == e2.KeyCode);

                    if (isMac && Settings.MacSwapCtrlWin) {
                        if (KeycodeV2.ModifiersControl.Contains(e2.KeyCode))
                            keykaseya = keywin;
                        else if (e2.KeyCode == System.Windows.Forms.Keys.LWin)
                            keykaseya = keyctrl;
                    }

                    if (keykaseya == null)
                        throw new KeyNotFoundException(e2.KeyCode.ToString());

                    bool removed = (keykaseya.IsMod ? listHeldKeysMod.Remove(keykaseya) : listHeldKeysOther.Remove(keykaseya));

                    rc.SendKeyUp(keykaseya.JavascriptKeyCode, keykaseya.USBKeyCode);

                    if (keyDownWin && !isMac) {
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
            if (!controlEnabled || rc == null || isMac)
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

            /*
            // !! TEST
            float currentAspectRatio = (float)glControl.Width / (float)glControl.Height;
            float targetAspectRatio = (float)virtualViewWant.Width / (float)virtualViewWant.Height;

            strKeyboard = string.Format("Need: {0}\r\nWant: {1}\r\nScale: {2:0.###}, {3:0.###}\r\nCam Trans: {4}\r\nCam Scale: {5}\r\nRatio: {6}\r\nor {7}",
                virtualViewNeed.ToString(), virtualViewWant.ToString(),
                scaleX, scaleY, MainCamera.Position.ToString(), MainCamera.Scale.X, currentAspectRatio, targetAspectRatio);
            // !! TEST
            */

            using (Graphics gfx = Graphics.FromImage(overlay2dKeyboard)) {
                gfx.Clear(System.Drawing.Color.Transparent);

                using (GraphicsPath gp = new GraphicsPath())
                using (System.Drawing.Pen outline = new System.Drawing.Pen(System.Drawing.Color.Black, 4) { LineJoin = LineJoin.Round }) //outline width=1
                using (StringFormat sf = new StringFormat())
                using (System.Drawing.Brush foreBrush = new SolidBrush(System.Drawing.Color.Lime)) {
                    gp.AddString(strKeyboard, arial.FontFamily, (int)arial.Style, arial.Size, new PointF(0, 0), sf); //Change arial.Size to 12 for the test text above
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
