﻿using NTR;
using OpenTK.Wpf;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using System;
using System.Windows;
using System.Windows.Input;
using Rectangle = System.Drawing.Rectangle;

namespace KLC_Finch {

    /// <summary>
    /// Interaction logic for RCvOpenGL.xaml
    /// OpenGLWPF does not work with RenderDoc for debugging.
    /// </summary>
    public partial class RCvOpenGLWPF : RCv {

        private readonly int[] m_shader_sampler = new int[3];
        private readonly Camera MainCamera;
        private int fragment_shader_object = 0;
        private string glVersion;
        private int m_shader_multiplyColor = 0;
        private double scaleX, scaleY;
        private int shader_program = 0;
        private int VBOScreen;
        private Vector2[] vertBufferScreen;
        private int vertex_shader_object = 0;
        private int vpX, vpY;

        private bool tempPanning;
        private System.Windows.Point tempPanningPoint;

        public RCvOpenGLWPF(IRemoteControl rc, RCstate state) : base(rc, state) {
            InitializeComponent();
            MainCamera = new Camera(Vector2.Zero); //for Multi-screen

            txtDebugLeft.Text = "";
            txtDebugRight.Text = "";
            //txtRcConnecting.Visibility = Visibility.Visible; //Default
            txtRcFrozen.Visibility = Visibility.Collapsed;
            txtRcDisconnected.Visibility = Visibility.Collapsed;

            bool glSupported = true;
            if (System.Windows.Media.RenderCapability.Tier < 0x00020000) {
                //Software, such as RDP
                //GLWpfControl would crash if used without the minimum version

                OpenGLSoftwareTest glSoftwareTest = new OpenGLSoftwareTest(50, 50, "OpenGL Test");
                glVersion = glSoftwareTest.Version;
                if (glVersion.StartsWith("1.") || glVersion.Contains("Mesa")) //Mesa seen on Citrix which claims to support 3.1
                    glSupported = false;
            }

            if (glSupported) {
                GLWpfControlSettings glSettings = new GLWpfControlSettings { MajorVersion = 3, MinorVersion = 1, RenderContinuously = true };
                try {
                    glControl.Start(glSettings);
                } catch (Exception ex) {
                    new WindowException(ex, "OpenGL Check").ShowDialog();
                    //glSupported = false;
                }
            }
        }

        public bool powerSaving { get; protected set; }
        public override bool SupportsLegacy { get { return true; } }
        public override void CameraFromClickedScreen(RCScreen screen, bool moveCamera = true) {
            if (state.UseMultiScreen && moveCamera)
                CameraToCurrentScreen();
        }

        public override void CameraToCurrentScreen() {
            if (!state.UseMultiScreen || state.CurrentScreen == null)
                return;

            state.UseMultiScreenOverview = false;

            ResetCamera();
            //DebugKeyboard();

            if (App.Settings.MultiAltFit) {
                bool adjustLeft = false;
                bool adjustUp = false;
                bool adjustRight = false;
                bool adjustDown = false;

                foreach (RCScreen screen in state.ListScreen) {
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

                state.SetVirtual(state.CurrentScreen.rect.X - (adjustLeft ? 80 : 0),
                    state.CurrentScreen.rect.Y - (adjustUp ? 80 : 0),
                    state.CurrentScreen.rect.Width + (adjustLeft ? 80 : 0) + (adjustRight ? 80 : 0),
                    state.CurrentScreen.rect.Height + (adjustUp ? 80 : 0) + (adjustDown ? 80 : (rc.IsMac ? 5 : 0)));
            } else
                state.SetVirtual(state.CurrentScreen.rect.X, state.CurrentScreen.rect.Y, state.CurrentScreen.rect.Width, state.CurrentScreen.rect.Height + (rc.IsMac ? 5 : 0));
        }

        public override void CameraToOverview() {
            if (!state.UseMultiScreen)
                return;

            state.UseMultiScreenOverview = true;

            int lowestX = 0;
            int lowestY = 0;
            int highestX = 0;
            int highestY = 0;
            foreach (RCScreen screen in state.ListScreen) {
                lowestX = Math.Min(screen.rect.X, lowestX);
                lowestY = Math.Min(screen.rect.Y, lowestY);
                highestX = Math.Max(screen.rect.X + screen.rect.Width, highestX);
                highestY = Math.Max(screen.rect.Y + screen.rect.Height, highestY);
            }

            SetCanvas(lowestX, lowestY, highestX, highestY);

            //--

            //MainCamera.Rotation = 0f;
            ResetCamera();
            //DebugKeyboard();

            state.virtualViewWant = state.virtualCanvas;
            state.virtualRequireViewportUpdate = true;
        }

        public override void MoveDown() {
            MainCamera.Move(new Vector2(0f, state.virtualViewWant.Height / 100));
        }

        public override void MoveLeft() {
            MainCamera.Move(new Vector2(-(state.virtualViewWant.Width / 100), 0f));
        }

        public override void MoveRight() {
            MainCamera.Move(new Vector2(state.virtualViewWant.Width / 100, 0f));
        }

        public override void MoveUp() {
            MainCamera.Move(new Vector2(0f, -(state.virtualViewWant.Height / 100)));
        }

        public override void TogglePanZoom() {
            state.UseMultiScreenPanZoom = !state.UseMultiScreenPanZoom;
        }

        /*
        public override void ZoomIn() {
            state.ZoomIn();
        }

        public override void ZoomOut() {
            state.ZoomOut();
        }
        */

        public override void CheckHealth() {
            txtDebugLeft.Visibility = (App.Settings.DisplayOverlayKeyboardMod || App.Settings.DisplayOverlayKeyboardOther ? Visibility.Visible : Visibility.Collapsed);
            txtDebugRight.Visibility = (App.Settings.DisplayOverlayMouse ? Visibility.Visible : Visibility.Collapsed);

            switch (state.connectionStatus) {
                case ConnectionStatus.FirstConnectionAttempt:
                    txtRcFrozen.Visibility = Visibility.Collapsed;
                    txtRcConnecting.Visibility = Visibility.Visible;
                    break;

                case ConnectionStatus.Connected:
                    txtRcConnecting.Visibility = Visibility.Collapsed;
                    /*
                    if (state.fpsCounter.SeemsAlive(5000)) {
                        txtRcFrozen.Visibility = Visibility.Collapsed;
                    } else {
                        txtRcFrozen.Visibility = Visibility.Visible;
                    }
                    */
                    break;

                case ConnectionStatus.Disconnected:
                    txtRcControlOff1.Visibility = txtRcControlOff2.Visibility = txtRcNotify.Visibility = Visibility.Collapsed;
                    txtRcFrozen.Visibility = Visibility.Collapsed;
                    txtRcDisconnected.Visibility = Visibility.Visible;
                    //rcBorderBG.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Maroon);

                    /*
                    if (state.keyHook.IsActive) {
                        keyHook.Uninstall();
                        txtRcHookOn.Visibility = Visibility.Collapsed;
                    }
                    */
                    break;
            }
        }

        public override void ControlLoaded(IRemoteControl rc, RCstate state) {
            this.rc = rc;
            this.state = state;

            //Legacy
            if (!state.UseMultiScreen)
                state.SetVirtual(0, 0, state.virtualWidth, state.virtualHeight);

            //--

            if (App.Settings.RendererAlt) {
                rc.DecodeMode = DecodeMode.BitmapRGB;
                state.Window.Title = state.BaseTitle + " (RGB)";
            } else {
                rc.DecodeMode = DecodeMode.RawYUV;
                state.Window.Title = state.BaseTitle + " (YUV)";
            }

            glVersion = GL.GetString(StringName.Version);

            CreateShaders(Shaders.yuvtorgb_vertex, Shaders.yuvtorgb_fragment, out vertex_shader_object, out fragment_shader_object, out shader_program);
            m_shader_sampler[0] = GL.GetUniformLocation(shader_program, "y_sampler");
            m_shader_sampler[1] = GL.GetUniformLocation(shader_program, "u_sampler");
            m_shader_sampler[2] = GL.GetUniformLocation(shader_program, "v_sampler");
            m_shader_multiplyColor = GL.GetUniformLocation(shader_program, "multiplyColor");

            InitTextures();
            RefreshVirtual();

            glControl.SizeChanged += GlControl_SizeChanged;
            glControl.MouseMove += HandleMouseMove;
            glControl.MouseDown += HandleMouseDown;
            glControl.MouseUp += HandleMouseUp;
            glControl.MouseLeave += HandleMouseLeave;
            glControl.MouseWheel += HandleMouseWheel;
            glControl.Render += GlControl_Render;
        }

        public override void ControlUnload() {
            if (shader_program != 0)
                GL.DeleteProgram(shader_program);
            if (fragment_shader_object != 0)
                GL.DeleteShader(fragment_shader_object);
            if (vertex_shader_object != 0)
                GL.DeleteShader(vertex_shader_object);
        }

        public override void DisplayApproval(bool visible) {
            Dispatcher.Invoke((Action)delegate {
                if (visible) {
                    txtRcConnecting.Visibility = Visibility.Collapsed;
                } else {
                    txtRcNotify.Visibility = Visibility.Collapsed;
                }
            });
        }

        public override void DisplayControl(bool enabled) {
            txtRcControlOff1.Visibility = txtRcControlOff2.Visibility = (state.ControlEnabled ? Visibility.Hidden : Visibility.Visible);
        }

        public override void DisplayDebugKeyboard(string strKeyboard) {
            Dispatcher.Invoke((Action)delegate {
                txtDebugLeft.Text = strKeyboard;
            });
        }

        public override void DisplayDebugMouseEvent(int X, int Y) {
            string strMousePos = string.Format("X: {0}, Y: {1}", X, Y);
            Dispatcher.Invoke((Action)delegate {
                txtDebugRight.Text = strMousePos;
            });
        }

        public override void DisplayKeyHook(bool enabled) {
            txtRcHookOn.Visibility = (enabled ? Visibility.Visible : Visibility.Hidden);
        }

        public override void ParentStateChange(bool visible) {
            if (visible && powerSaving) {
                powerSaving = false;
                //glControl.Render += GlControl_Render;
            } else {
                powerSaving = true;
                //glControl.Render -= GlControl_Render;
            }
        }

        public override void Refresh() {
        }
        
        public override void SetCanvas(int virtualX, int virtualY, int virtualWidth, int virtualHeight) { //More like lowX, lowY, highX, highY
            if (state.UseMultiScreen) {
                state.virtualCanvas = new Rectangle(virtualX, virtualY, Math.Abs(virtualX) + virtualWidth, Math.Abs(virtualY) + virtualHeight);
                state.SetVirtual(virtualX, virtualY, state.virtualCanvas.Width, state.virtualCanvas.Height);
            } else {
                state.virtualCanvas = new Rectangle(0, 0, virtualWidth, virtualHeight);
                state.SetVirtual(0, 0, virtualWidth, virtualHeight);
            }

            state.virtualRequireViewportUpdate = true;
        }

        public override bool SwitchToLegacy() {
            state.UseMultiScreen = false;
            state.virtualRequireViewportUpdate = true;

            return true;
        }

        public override bool SwitchToMultiScreen() {
            state.UseMultiScreen = true;
            state.virtualRequireViewportUpdate = true;

            return true;
        }

        public override void UpdateScreenLayout(int lowestX, int lowestY, int highestX, int highestY) {
            //Empty
        }

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

        private System.Drawing.Color ConnectionStatusToColor()
        {
            switch (state.connectionStatus)
            {
                case ConnectionStatus.FirstConnectionAttempt:
                    return System.Drawing.Color.SlateGray;

                case ConnectionStatus.Connected:
                    if (state.ControlEnabled)
                        return System.Drawing.Color.FromArgb(255, 20, 20, 20);
                    else
                        return System.Drawing.Color.MidnightBlue;

                case ConnectionStatus.Disconnected:
                    return System.Drawing.Color.Maroon;
            }

            return System.Drawing.Color.BlueViolet;
        }

        private void GlControl_Render(TimeSpan obj) {
            if (state.UseMultiScreen)
                RenderStartMulti();
            else
                RenderStartLegacy();

            //--

            //GL.UseProgram(0); //No shader for RGB

            // Setup new textures, not actually render
            //lock (lockFrameBuf) {
            foreach (RCScreen screen in state.ListScreen) {
                if (screen.Texture == null)
                    screen.Texture = new TextureScreen(rc.DecodeMode);
                else
                    screen.Texture.RenderNew(m_shader_sampler);
            }
            if (state.UseMultiScreen) {
                if (state.textureCursor == null) {
                    state.textureCursor = new TextureCursor();
                } else
                    state.textureCursor.RenderNew();
            }
            if (!state.UseMultiScreen) {
                if (state.textureLegacy != null)
                    state.textureLegacy.RenderNew(m_shader_sampler);
            }
            //}

            System.Drawing.Color bgColor = ConnectionStatusToColor();
            GL.ClearColor(bgColor);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.Finish(); //Why is this here?

            if (state.UseMultiScreen) {
                for (int i = 0; i < state.ListScreen.Count; i++) {
                    if (state.ListScreen[i].Texture != null) {
                        System.Drawing.Color multiplyColor;
                        if (state.ListScreen[i] == state.CurrentScreen)
                            multiplyColor = System.Drawing.Color.White;
                        else if (state.ControlEnabled || state.UseMultiScreenOverview) //In overview, or it's on the edge of focused screen
                            multiplyColor = System.Drawing.Color.Gray;
                        else
                            multiplyColor = System.Drawing.Color.Cyan;

                        if (!state.ListScreen[i].Texture.Render(shader_program, m_shader_sampler, m_shader_multiplyColor, multiplyColor)) {
                            GL.Disable(EnableCap.Texture2D);
                            GL.UseProgram(0);
                            GL.Color3(System.Drawing.Color.DimGray);

                            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                            GL.Begin(PrimitiveType.Polygon);
                            GL.PointSize(5f);
                            GL.LineWidth(5f);

                            GL.Vertex2(state.ListScreen[i].rect.Left, state.ListScreen[i].rect.Bottom);
                            GL.Vertex2(state.ListScreen[i].rect.Left, state.ListScreen[i].rect.Top);
                            GL.Vertex2(state.ListScreen[i].rect.Right, state.ListScreen[i].rect.Top);
                            GL.Vertex2(state.ListScreen[i].rect.Right, state.ListScreen[i].rect.Bottom);

                            //GL.Vertex2(vertBufferScreen[0].X, vertBufferScreen[0].Y);

                            GL.End();
                        }
                    }
                }

                if (App.Settings.MultiShowCursor) {
                    if (state.textureCursor != null) {
                        GL.Color3(System.Drawing.Color.White);
                        state.textureCursor.Render();
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
                if (!state.textureLegacy.Render(shader_program, m_shader_sampler, m_shader_multiplyColor, System.Drawing.Color.White)) {
                    GL.Disable(EnableCap.Texture2D);
                    GL.UseProgram(0);
                    GL.Color3(System.Drawing.Color.DimGray);

                    //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                    GL.Begin(PrimitiveType.Polygon);
                    GL.PointSize(5f);
                    GL.LineWidth(5f);

                    GL.Vertex2(0, state.legacyVirtualHeight);
                    GL.Vertex2(0, 0);
                    GL.Vertex2(state.legacyVirtualWidth, 0);
                    GL.Vertex2(state.legacyVirtualWidth, state.legacyVirtualHeight);

                    //GL.Vertex2(vertBufferScreen[0].X, vertBufferScreen[0].Y);

                    GL.End();
                }
            }

            GL.Finish();
        }

        private void GLResetShaderAndTextures()
        {
            //For whatever reason, this is only a problem for OpenGLWPF

            GL.UseProgram(0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture1);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture2);
            GL.BindTexture(TextureTarget.Texture2D, 0);
            GL.ActiveTexture(TextureUnit.Texture3);
            GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        private void GlControl_SizeChanged(object sender, SizeChangedEventArgs e) {
            //Does this do anything anymore?
            RefreshVirtual();
            state.virtualRequireViewportUpdate = true;
        }
        
        private void HandleMouseDown(object sender, MouseButtonEventArgs e) {
            if (state.connectionStatus != ConnectionStatus.Connected)
                return;

            System.Windows.Point pointWPF = e.GetPosition(glControl);
            if (state.UseMultiScreen) {
                if (!state.ControlEnabled && e.ChangedButton == MouseButton.Right)
                {
                    if (state.UseMultiScreenPanZoom)
                    {
                        tempPanningPoint = pointWPF;
                        tempPanning = true;
                    }
                    return;
                }

                Vector2 point = MainCamera.ScreenToWorldCoordinates(new Vector2((float)(pointWPF.X / scaleX), (float)(pointWPF.Y / scaleY)), state.virtualViewNeed.X, state.virtualViewNeed.Y);
                RCScreen screenPointingTo = state.GetScreenUsingMouse((int)point.X, (int)point.Y);
                if (screenPointingTo == null)
                    return;

                if (state.ControlEnabled) {
                    if (!state.UseMultiScreenPanZoom && screenPointingTo != state.CurrentScreen) {
                        state.Window.FromGlChangeScreen(screenPointingTo, true);
                        return;
                    }
                } else {
                    if (e.ClickCount == 2) {
                        state.Window.SetControlEnabled(true);
                    } else if (e.ChangedButton == MouseButton.Left) {
                        if (!state.UseMultiScreenPanZoom) {
                            if (state.CurrentScreen != screenPointingTo) //Multi-Screen (Focused), Control Disabled, Change Screen
                                state.Window.FromGlChangeScreen(screenPointingTo, false);
                            //Else
                            //We already changed the active screen by moving the mouse
                            CameraToCurrentScreen();
                        }
                    }

                    return;
                }
            } else {
                //Use legacy behavior

                if (!state.ControlEnabled) {
                    if (e.ClickCount == 2)
                        state.Window.SetControlEnabled(true);

                    return;
                }
            }

            if (e.ChangedButton == MouseButton.Middle) {
                if (e.ClickCount == 1) //Logitech bug
                    state.Window.PerformAutotype(false);
            } else {
                if (state.windowActivatedMouseMove)
                    HandleMouseMove(sender, e);

                rc.SendMouseDown(e.ChangedButton);

                if (e.ChangedButton == MouseButton.Left)
                    state.mouseHeldLeft = true;
                if (e.ChangedButton == MouseButton.Right)
                    state.mouseHeldRight = true;

                state.Window.DebugKeyboard();
            }
        }

        private void HandleMouseLeave(object sender, MouseEventArgs e)
        {
            tempPanning = false;
        }

        private void HandleMouseMove(object sender, MouseEventArgs e) {
            if (state.CurrentScreen == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            state.windowActivatedMouseMove = false;
            System.Windows.Point pointWPF = e.GetPosition(glControl);

            if (state.UseMultiScreen) {
                if (!state.ControlEnabled)
                {
                    if (tempPanning)
                    {
                        Vector2 diff = new Vector2((float)(tempPanningPoint.X - pointWPF.X), (float)(tempPanningPoint.Y - pointWPF.Y));
                        tempPanningPoint = pointWPF;
                        MainCamera.Move(diff);
                        return;
                    }
                }

                Vector2 point = MainCamera.ScreenToWorldCoordinates(new Vector2((float)(pointWPF.X / scaleX), (float)(pointWPF.Y / scaleY)), state.virtualViewNeed.X, state.virtualViewNeed.Y);

                RCScreen screenPointingTo = state.GetScreenUsingMouse((int)point.X, (int)point.Y);
                if (screenPointingTo == null)
                    return;

                if ((state.UseMultiScreenOverview || state.UseMultiScreenPanZoom) && state.CurrentScreen.screen_id != screenPointingTo.screen_id) {
                    //We are in overview, change which screen gets texture updates
                    state.Window.FromGlChangeScreen(screenPointingTo, false);

                    //previousScreen = currentScreen;
                    //currentScreen = screenPointingTo;
                    //rc.ChangeScreen(currentScreen.screen_id);
                }

                if (!state.ControlEnabled || !state.WindowIsActive())
                    return;

                state.Window.DebugMouseEvent((int)point.X, (int)point.Y);
                rc.SendMousePosition((int)point.X, (int)point.Y);
            } else {
                //Legacy behavior
                if (!state.ControlEnabled || !state.WindowIsActive())
                    return;

                System.Drawing.Point legacyPoint = new System.Drawing.Point((int)pointWPF.X - vpX, (int)pointWPF.Y - vpY);
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

                if (legacyPoint.X > state.legacyVirtualWidth || legacyPoint.Y > state.legacyVirtualHeight)
                    return;

                legacyPoint.X += state.CurrentScreen.rect.X;
                legacyPoint.Y += state.CurrentScreen.rect.Y;

                state.Window.DebugMouseEvent(legacyPoint.X, legacyPoint.Y);
                rc.SendMousePosition(legacyPoint.X, legacyPoint.Y);
            }
        }

        private void HandleMouseUp(object sender, MouseButtonEventArgs e) {
            if (state.connectionStatus != ConnectionStatus.Connected)
                return;

            if (!state.ControlEnabled)
            {
                if (e.ChangedButton == MouseButton.Right)
                    tempPanning = false;
                return;
            }

            if (glControl.IsMouseOver) {
                rc.SendMouseUp(e.ChangedButton);

                if (e.ChangedButton == MouseButton.Left)
                    state.mouseHeldLeft = false;
                if (e.ChangedButton == MouseButton.Right)
                    state.mouseHeldRight = false;

                state.Window.DebugKeyboard();
            }
        }

        private void HandleMouseWheel(object sender, MouseWheelEventArgs e) {
            if (rc == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            if (state.ControlEnabled)
                rc.SendMouseWheel(e.Delta);
            else if (state.UseMultiScreenPanZoom) {
                //Console.WriteLine(e.Delta + " " + MainCamera.Scale);
                if (e.Delta > 0) {
                    MainCamera.Scale = Vector2.Add(MainCamera.Scale, new Vector2(0.1f, 0.1f));
                    if (MainCamera.Scale.X > 4.0 || MainCamera.Scale.Y > 4.0)
                        MainCamera.Scale = new Vector2(4.0f, 4.0f);
                } else {
                    MainCamera.Scale = Vector2.Subtract(MainCamera.Scale, new Vector2(0.1f, 0.1f));
                    if (MainCamera.Scale.X < 0.1 || MainCamera.Scale.Y < 0.1)
                        MainCamera.Scale = new Vector2(0.1f, 0.1f);
                }
            }
        }

        private void InitTextures() {
            state.textureLegacy = new TextureScreen(rc.DecodeMode);
            //InitLegacyScreenTexture();

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.Enable(EnableCap.Texture2D);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        private void RefreshVirtual() {
            vertBufferScreen = new Vector2[8] {
                new Vector2(state.virtualCanvas.Left, state.virtualCanvas.Top), new Vector2(0, 1),
                new Vector2(state.virtualCanvas.Right, state.virtualCanvas.Top), new Vector2(1, 1),
                new Vector2(state.virtualCanvas.Right, state.virtualCanvas.Bottom), new Vector2(1, 0),
                new Vector2(state.virtualCanvas.Left, state.virtualCanvas.Bottom), new Vector2(0, 0)
            };

            VBOScreen = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, VBOScreen);
            GL.BufferData<Vector2>(BufferTarget.ArrayBuffer, (IntPtr)(Vector2.SizeInBytes * vertBufferScreen.Length), vertBufferScreen, BufferUsageHint.StaticDraw);
        }

        private void RenderStartLegacy() {
            if (state.virtualRequireViewportUpdate) {
                RefreshVirtual();
                state.virtualRequireViewportUpdate = false;
            }

            float targetAspectRatio = (float)state.legacyVirtualWidth / (float)state.legacyVirtualHeight;

            int width = (int)glControl.FrameBufferWidth;
            int height = (int)((float)width / targetAspectRatio/* + 0.5f*/);

            if (height > glControl.FrameBufferHeight) {
                //Pillarbox
                height = glControl.FrameBufferHeight;
                width = (int)((float)height * targetAspectRatio/* + 0.5f*/);
            }

            vpX = (glControl.FrameBufferWidth / 2) - (width / 2);
            vpY = (glControl.FrameBufferHeight / 2) - (height / 2);
            GL.Viewport(vpX, vpY, width, height);

            //Yay DPI, these values are used for mouse position
            scaleX = glControl.ActualWidth / glControl.FrameBufferWidth;
            scaleY = glControl.ActualHeight / glControl.FrameBufferHeight;
            vpX = (int)(vpX * scaleX);
            vpY = (int)(vpY * scaleY);

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, state.legacyVirtualWidth, state.legacyVirtualHeight, 0, -1, 1);//Upsidedown
            //GL.Ortho(0, legacyVirtualWidth, legacyVirtualHeight, 0, -1, 1);//Upsidedown
            //GL.Ortho(0, legacyVirtualWidth, 0, legacyVirtualHeight, -1, 1);

            GL.MatrixMode(MatrixMode.Modelview);
            //GL.PushMatrix();

            //Now to calculate the scale considering the screen size and virtual size
            scaleX = (double)glControl.ActualWidth / (double)state.legacyVirtualWidth;
            scaleY = (double)glControl.ActualHeight / (double)state.legacyVirtualHeight;
            //GL.Scale(scaleX, scaleY, 1.0f);

            GL.LoadIdentity();

            GL.Disable(EnableCap.DepthTest);
        }

        private void RenderStartMulti() {
            if (state.virtualRequireViewportUpdate) {
                float currentAspectRatio = (float)glControl.FrameBufferWidth / (float)glControl.FrameBufferHeight;
                float targetAspectRatio = (float)state.virtualViewWant.Width / (float)state.virtualViewWant.Height;
                int width = state.virtualViewWant.Width;
                int height = state.virtualViewWant.Height;
                vpX = 0;
                vpY = 0;

                if (currentAspectRatio > targetAspectRatio) {
                    //Pillarbox
                    width = (int)((float)height * currentAspectRatio);
                    vpX = (width - state.virtualViewWant.Width) / 2;
                } else {
                    //Letterbox
                    height = (int)((float)width / currentAspectRatio);
                    vpY = (height - state.virtualViewWant.Height) / 2;
                }

                scaleX = glControl.ActualWidth / (double)width;
                scaleY = glControl.ActualHeight / (double)height;

                state.virtualViewNeed = new Rectangle(state.virtualViewWant.X - vpX, state.virtualViewWant.Y - vpY, width, height);

                state.virtualRequireViewportUpdate = false;
            }

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(state.virtualViewNeed.Left, state.virtualViewNeed.Right, state.virtualViewNeed.Bottom, state.virtualViewNeed.Top, MainCamera.ZNear, MainCamera.ZFar);
            GL.Viewport(0, 0, (int)glControl.FrameBufferWidth, (int)glControl.FrameBufferHeight);
            MainCamera.ApplyTransform();
            GL.MatrixMode(MatrixMode.Modelview);
        }

        public override void ResetCamera()
        {
            //MainCamera.Rotation = 0f;
            MainCamera.Position = Vector2.Zero;
            MainCamera.Scale = new Vector2(1f, 1f);
        }
    }
}