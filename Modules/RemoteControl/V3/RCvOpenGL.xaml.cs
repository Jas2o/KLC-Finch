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
    /// <summary>
    /// Interaction logic for RCvOpenGL.xaml
    /// </summary>
    public partial class RCvOpenGL : RCv {

        public bool powerSaving { get; protected set; }

        private string glVersion;
        private int shader_program = 0;
        private int m_shader_multiplyColor = 0;
        private readonly int[] m_shader_sampler = new int[3];
        private int fragment_shader_object = 0;
        private int vertex_shader_object = 0;

        private readonly Camera MainCamera;
        private int vpX, vpY;
        private double scaleX, scaleY;

        private const int overlayHeight = 100;
        private const int overlayWidth = 400;
        private static Font arial = new Font("Arial", 32);
        private Bitmap overlay2dControlOff;
        private Bitmap overlay2dDisconnected;
        private Bitmap overlay2dKeyboard;
        private Bitmap overlay2dMouse;
        private int textureOverlay2dControlOff;
        private int textureOverlay2dDisconnected;
        private int textureOverlay2dKeyboard;
        private int textureOverlay2dMouse;
        private byte[] textureOverlayDataControlOff;
        private byte[] textureOverlayDataDisconnected;
        private byte[] textureOverlayDataKeyboard;
        private byte[] textureOverlayDataMouse;
        private int VBOScreen;
        private int VBOmouse, VBOkeyboard, VBOtop, VBOcenter;
        private Vector2[] vertBufferScreen;
        private Vector2[] vertBufferMouse, vertBufferKeyboard, vertBufferTop, vertBufferCenter;

        public override bool SupportsLegacy { get { return true; } }
        public override bool SupportsZoom { get { return true; } }

        public RCvOpenGL(IRemoteControl rc, RCstate state) : base(rc, state) {
            InitializeComponent();
            MainCamera = new Camera(Vector2.Zero); //for Multi-screen
            glControl.Load += glControl_Load;
        }

        public override void Refresh() {
            glControl.Invalidate();
        }

        public override void ParentStateChange(bool visible) {
            if(visible && powerSaving) {
                powerSaving = false;
                //glControl.Render += GlControl_Render;
            } else {
                powerSaving = true;
                //glControl.Render -= GlControl_Render;
            }
        }

        public override void DisplayApproval(bool visible) {
            Dispatcher.Invoke((Action)delegate {
                txtRcNotify.Visibility = (visible ? Visibility.Visible : Visibility.Collapsed);
            });
        }

        public override void DisplayControl(bool enabled) {

        }

        public override void DisplayKeyHook(bool enabled) {
            //txtRcHookOn.Visibility = (enabled ? Visibility.Visible : Visibility.Hidden);
        }

        public override void DisplayDebugKeyboard(string strKeyboard) {
        }

        public override void DisplayDebugMouseEvent(int X, int Y) {
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
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                //gfx.Clear(System.Drawing.Color.Transparent);
                gfx.Clear(System.Drawing.Color.FromArgb(128, 0, 0, 0));

                using (GraphicsPath gp = new GraphicsPath())
                using (System.Drawing.Pen outline = new System.Drawing.Pen(System.Drawing.Color.Black, 3) { LineJoin = LineJoin.Round }) //outline width=1
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
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.Clear(System.Drawing.Color.Transparent);

                using (GraphicsPath gp = new GraphicsPath())
                using (System.Drawing.Pen outline = new System.Drawing.Pen(Color.FromArgb(128, Color.Black), 3) { LineJoin = LineJoin.Round }) //outline width=1
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

            state.textureLegacy = new TextureScreen(rc.DecodeMode);
            //InitLegacyScreenTexture();

            GL.EnableClientState(ArrayCap.VertexArray);
            GL.EnableClientState(ArrayCap.TextureCoordArray);
            GL.Enable(EnableCap.Texture2D);

            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        }

        private void glControl_Paint(object sender, System.Windows.Forms.PaintEventArgs e) {
            //This happens when initialized and when resized
            Render();
        }

        private void RenderStartLegacy() {
            if (state.virtualRequireViewportUpdate) {
                RefreshVirtual();
                state.virtualRequireViewportUpdate = false;
            }

            int screenWidth = glControl.Width;
            int screenHeight = glControl.Height;

            float targetAspectRatio = (float)state.legacyVirtualWidth / (float)state.legacyVirtualHeight;

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
            GL.Ortho(0, state.legacyVirtualWidth, state.legacyVirtualHeight, 0, -1, 1);//Upsidedown
            //GL.Ortho(0, legacyVirtualWidth, 0, legacyVirtualHeight, -1, 1);

            GL.MatrixMode(MatrixMode.Modelview);
            //GL.PushMatrix();

            //Now to calculate the scale considering the screen size and virtual size
            scaleX = (double)screenWidth / (double)state.legacyVirtualWidth;
            scaleY = (double)screenHeight / (double)state.legacyVirtualHeight;
            GL.Scale(scaleX, scaleY, 1.0f);

            GL.LoadIdentity();

            GL.Disable(EnableCap.DepthTest);
        }

        private void RenderStartMulti() {
            if (state.virtualRequireViewportUpdate) {
                float currentAspectRatio = (float)glControl.Width / (float)glControl.Height;
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

                scaleX = (double)glControl.Width / (double)width;
                scaleY = (double)glControl.Height / (double)height;

                state.virtualViewNeed = new Rectangle(state.virtualViewWant.X - vpX, state.virtualViewWant.Y - vpY, width, height);

                state.virtualRequireViewportUpdate = false;
            }

            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(state.virtualViewNeed.Left, state.virtualViewNeed.Right, state.virtualViewNeed.Bottom, state.virtualViewNeed.Top, MainCamera.ZNear, MainCamera.ZFar);
            GL.Viewport(0, 0, glControl.Width, glControl.Height);
            MainCamera.ApplyTransform();
            GL.MatrixMode(MatrixMode.Modelview);
        }

        private void Render() {
            glControl.MakeCurrent();

            if (state.useMultiScreen)
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
            if (state.useMultiScreen) {
                if (state.textureCursor == null) {
                    state.textureCursor = new TextureCursor();
                } else
                    state.textureCursor.RenderNew();
            }
            if (!state.useMultiScreen) {
                if (state.textureLegacy != null)
                    state.textureLegacy.RenderNew(m_shader_sampler);
            }
            //}

            switch (state.connectionStatus) {
                case ConnectionStatus.FirstConnectionAttempt:
                    GL.ClearColor(System.Drawing.Color.SlateGray);
                    break;

                case ConnectionStatus.Connected:
                    if (state.controlEnabled)
                        GL.ClearColor(Color.FromArgb(255, 20, 20, 20));
                    else
                        GL.ClearColor(System.Drawing.Color.MidnightBlue);
                    break;

                case ConnectionStatus.Disconnected:
                    GL.ClearColor(System.Drawing.Color.Maroon);
                    break;
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            if (state.useMultiScreen) {
                foreach (RCScreen screen in state.ListScreen) {
                    if (screen.Texture != null) {
                        Color multiplyColor;
                        if (screen == state.CurrentScreen)
                            multiplyColor = Color.White;
                        else if (state.controlEnabled || state.virtualViewWant == state.virtualCanvas) //In overview, or it's on the edge of focused screen
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

                if (App.Settings.MultiShowCursor) {
                    if (state.textureCursor != null) {
                        GL.Color3(Color.White);
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
                if (!state.textureLegacy.Render(shader_program, m_shader_sampler, m_shader_multiplyColor, Color.White)) {
                    GL.Disable(EnableCap.Texture2D);
                    GL.UseProgram(0);
                    GL.Color3(Color.DimGray);

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

            //--

            GL.Enable(EnableCap.Texture2D);

            #region Overlay

            if (!state.useMultiScreen)
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
            if (App.Settings.DisplayOverlayMouse) {
                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(TextureTarget.Texture2D, textureOverlay2dMouse);
                GL.BindBuffer(BufferTarget.ArrayBuffer, VBOmouse);
                GL.VertexPointer(2, VertexPointerType.Float, Vector2.SizeInBytes * 2, 0);
                GL.TexCoordPointer(2, TexCoordPointerType.Float, Vector2.SizeInBytes * 2, Vector2.SizeInBytes);
                GL.DrawArrays(PrimitiveType.Quads, 0, vertBufferMouse.Length / 2);
            }

            if (App.Settings.DisplayOverlayKeyboardOther || App.Settings.DisplayOverlayKeyboardMod) {
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
            } else if (state.connectionStatus == ConnectionStatus.Connected && !state.controlEnabled) {
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

        private void glControl_Resize(object sender, EventArgs e) {
            RefreshVirtual();
            state.virtualRequireViewportUpdate = true;
        }

        private void HandleMouseWheel(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!state.controlEnabled || rc == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.SendMouseWheel(e.Delta);
        }

        public override void ControlUnload() {
            if (shader_program != 0)
                GL.DeleteProgram(shader_program);
            if (fragment_shader_object != 0)
                GL.DeleteShader(fragment_shader_object);
            if (vertex_shader_object != 0)
                GL.DeleteShader(vertex_shader_object);
        }

        public override void SetCanvas(int virtualX, int virtualY, int virtualWidth, int virtualHeight) { //More like lowX, lowY, highX, highY
            if (state.useMultiScreen) {
                state.virtualCanvas = new Rectangle(virtualX, virtualY, Math.Abs(virtualX) + virtualWidth, Math.Abs(virtualY) + virtualHeight);
                state.SetVirtual(virtualX, virtualY, state.virtualCanvas.Width, state.virtualCanvas.Height);
            } else {
                state.virtualCanvas = new Rectangle(0, 0, virtualWidth, virtualHeight);
                state.SetVirtual(0, 0, virtualWidth, virtualHeight);
            }

            state.virtualRequireViewportUpdate = true;
        }

        public override void CameraFromClickedScreen(RCScreen screen, bool moveCamera = true) {
            if (state.useMultiScreen && moveCamera)
                CameraToCurrentScreen();
        }

        public override void ControlLoaded(IRemoteControl rc, RCstate state) {
            this.rc = rc;
            this.state = state;

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
        }

        public override void CameraToCurrentScreen() {
            if (!state.useMultiScreen)
                return;

            state.useMultiScreenOverview = false;

            //MainCamera.Rotation = 0f;
            MainCamera.Position = Vector2.Zero;
            MainCamera.Scale = new Vector2(1f, 1f);
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

                state.SetVirtual(state.CurrentScreen.rectFixed.X - (adjustLeft ? 80 : 0),
                    state.CurrentScreen.rectFixed.Y - (adjustUp ? 80 : 0),
                    state.CurrentScreen.rectFixed.Width + (adjustLeft ? 80 : 0) + (adjustRight ? 80 : 0),
                    state.CurrentScreen.rectFixed.Height + (adjustUp ? 80 : 0) + (adjustDown ? 80 : 0));
            } else
                state.SetVirtual(state.CurrentScreen.rectFixed.X, state.CurrentScreen.rectFixed.Y, state.CurrentScreen.rectFixed.Width, state.CurrentScreen.rectFixed.Height);
        }

        public override void CameraToOverview() {
            if (!state.useMultiScreen)
                return;

            state.useMultiScreenOverview = true;

            int lowestX = 0;
            int lowestY = 0;
            int highestX = 0;
            int highestY = 0;
            foreach (RCScreen screen in state.ListScreen) {
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

            state.virtualViewWant = state.virtualCanvas;
            state.virtualRequireViewportUpdate = true;
        }

        public override bool SwitchToMultiScreen() {
            return false;
        }
        public override bool SwitchToLegacy() {
            state.useMultiScreen = false;
            state.virtualRequireViewportUpdate = true;

            return true;
        }

        public override void UpdateScreenLayout(int lowestX, int lowestY, int highestX, int highestY) {
            //Empty
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

        private void HandleMouseDown(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (rc == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            if (state.useMultiScreen) {
                Vector2 point = MainCamera.ScreenToWorldCoordinates(new Vector2((float)(e.X / scaleX), (float)(e.Y / scaleY)), state.virtualViewNeed.X, state.virtualViewNeed.Y);
                RCScreen screenPointingTo = state.GetScreenUsingMouse((int)point.X, (int)point.Y);
                if (screenPointingTo == null)
                    return;

                if (state.controlEnabled) {
                    if (state.virtualViewWant != state.virtualCanvas && screenPointingTo != state.CurrentScreen) {
                        state.Window.FromGlChangeScreen(screenPointingTo, true);
                        return;
                    }
                } else {
                    if (e.Clicks == 2) {
                        state.Window.SetControlEnabled(true);
                    } else if (e.Button == System.Windows.Forms.MouseButtons.Left) {
                        if (state.CurrentScreen != screenPointingTo) //Multi-Screen (Focused), Control Disabled, Change Screen
                            state.Window.FromGlChangeScreen(screenPointingTo, false);
                        //Else
                        //We already changed the active screen by moving the mouse
                        CameraToCurrentScreen();
                    }

                    return;
                }
            } else {
                //Use legacy behavior

                if (!state.controlEnabled) {
                    if (e.Clicks == 2)
                        state.Window.SetControlEnabled(true);

                    return;
                }
            }

            if (e.Button == System.Windows.Forms.MouseButtons.Middle) {
                if (e.Clicks == 1) //Logitech bug
                    state.Window.PerformAutotype();
            } else {
                if (state.windowActivatedMouseMove)
                    HandleMouseMove(sender, e);

                rc.SendMouseDown(e.Button);

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    state.mouseHeldLeft = true;
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    state.mouseHeldRight = true;

                state.Window.DebugKeyboard();
            }
        }

        private void HandleMouseMove(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (state.CurrentScreen == null || rc == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            state.windowActivatedMouseMove = false;

            if (state.useMultiScreen) {
                Vector2 point = MainCamera.ScreenToWorldCoordinates(new Vector2((float)(e.X / scaleX), (float)(e.Y / scaleY)), state.virtualViewNeed.X, state.virtualViewNeed.Y);
                state.Window.DebugMouseEvent((int)point.X, (int)point.Y);

                RCScreen screenPointingTo = state.GetScreenUsingMouse((int)point.X, (int)point.Y);
                if (screenPointingTo == null)
                    return;

                if (state.virtualViewWant == state.virtualCanvas && state.CurrentScreen.screen_id != screenPointingTo.screen_id) {
                    //We are in overview, change which screen gets texture updates
                    state.Window.FromGlChangeScreen(screenPointingTo, false);

                    //previousScreen = currentScreen;
                    //currentScreen = screenPointingTo;
                    //rc.ChangeScreen(currentScreen.screen_id);
                }

                if (!state.controlEnabled || !state.WindowIsActive())
                    return;

                rc.SendMousePosition((int)point.X, (int)point.Y);
            } else {
                //Legacy behavior
                if (!state.controlEnabled || !state.WindowIsActive())
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

                if (legacyPoint.X > state.legacyVirtualWidth || legacyPoint.Y > state.legacyVirtualHeight)
                    return;

                legacyPoint.X = legacyPoint.X + state.CurrentScreen.rect.X;
                legacyPoint.Y = legacyPoint.Y + state.CurrentScreen.rect.Y;

                state.Window.DebugMouseEvent(legacyPoint.X, legacyPoint.Y);

                rc.SendMousePosition(legacyPoint.X, legacyPoint.Y);
            }
        }

        private void HandleMouseUp(object sender, System.Windows.Forms.MouseEventArgs e) {
            if (!state.controlEnabled || rc == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            if (glControl.ClientRectangle.Contains(e.Location)) {
                rc.SendMouseUp(e.Button);

                if (e.Button == System.Windows.Forms.MouseButtons.Left)
                    state.mouseHeldLeft = false;
                if (e.Button == System.Windows.Forms.MouseButtons.Right)
                    state.mouseHeldRight = false;

                state.Window.DebugKeyboard();
            }
        }

        private void HandleMouseWheel(object sender, MouseWheelEventArgs e) {
            if (!state.controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.SendMouseWheel(e.Delta);
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

        public override void CheckHealth() {
            if (state.connectionStatus == ConnectionStatus.Disconnected)
                glControl.Invalidate();
        }
    }
}
