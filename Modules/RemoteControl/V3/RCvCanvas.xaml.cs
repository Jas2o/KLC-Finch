using NTR;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace KLC_Finch {

    /// <summary>
    /// Interaction logic for RCvCanvas.xaml
    /// </summary>
    public partial class RCvCanvas : RCv {
        private List<System.Windows.Shapes.Rectangle> canvasListRectangle;
        private int canvasOffsetX, canvasOffsetY;

        public RCvCanvas(IRemoteControl rc, RCstate state) : base(rc, state) {
            InitializeComponent();
            txtDebugLeft.Text = "";
            txtDebugRight.Text = "";
            //txtRcConnecting.Visibility = Visibility.Visible; //Default
            txtRcFrozen.Visibility = Visibility.Collapsed;
            txtRcDisconnected.Visibility = Visibility.Collapsed;
        }

        public override void Refresh() {
        }

        public override void SetCanvas(int virtualX, int virtualY, int virtualWidth, int virtualHeight) { //More like lowX, lowY, highX, highY
            //Canvasdidn't have anything active here

            //Dispatcher.Invoke((Action)delegate {
            //transformCanvas.Matrix.Reset
            //System.Windows.Media.Matrix matrix = transformCanvas.Matrix;

            //scaleX = rcCanvas.ActualWidth / (double)rcViewbox.ActualWidth;
            //scaleY = rcCanvas.ActualHeight / (double)rcViewbox.ActualHeight;

            //matrix.ScaleAt(scaleX, scaleY, virtualWidth, virtualHeight);
            //transformCanvas.Matrix = matrix;
            //rcCanvas.Width = virtualWidth;
            //rcCanvas.Height = virtualHeight;
            //});
        }

        public override void ControlUnload() {
        }

        public override void CameraFromClickedScreen(RCScreen screen, bool moveCamera = true) {
            if (state.useMultiScreen && moveCamera)
                SetCanvas(state.CurrentScreen.rect.X, state.CurrentScreen.rect.Y, state.CurrentScreen.rect.Width, state.CurrentScreen.rect.Height);
        }

        public override void ControlLoaded(IRemoteControl rc, RCstate state) {
            this.rc = rc;
            this.state = state;

            if (!state.useMultiScreen)
                state.SetVirtual(0, 0, state.virtualWidth, state.virtualHeight);

            //rcCanvas.Children.Clear();
            rcRectangleExample.Visibility = Visibility.Hidden;
            canvasListRectangle = new List<System.Windows.Shapes.Rectangle>();

            if (App.Settings.GraphicsModeV3 == GraphicsMode.Canvas_Y) {
                rc.DecodeMode = DecodeMode.RawY;
                state.Window.Title = state.BaseTitle + " (Canvas Y) Alpha";
            } else {
                rc.DecodeMode = DecodeMode.BitmapRGB;
                state.Window.Title = state.BaseTitle + " (Canvas RGB) Alpha";
            }

            rcBorderBG.MouseMove += HandleCanvasMouseMove;
            rcCanvas.MouseMove += HandleCanvasMouseMove;
            rcCanvas.MouseDown += HandleCanvasMouseDown;
            rcCanvas.MouseUp += HandleCanvasMouseUp;
            rcCanvas.MouseWheel += HandleCanvasMouseWheel;
        }

        public override void ParentStateChange(bool visible) {
        }

        public override void DisplayApproval(bool visible) {
            Dispatcher.Invoke((Action)delegate {
                txtRcNotify.Visibility = (visible ? Visibility.Visible : Visibility.Collapsed);
            });
        }

        public override void DisplayControl(bool controlEnabled) {
            if (state.controlEnabled)
                rcBorderBG.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 20, 20, 20));
            else
                rcBorderBG.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.MidnightBlue);

            txtRcControlOff1.Visibility = txtRcControlOff2.Visibility = (state.controlEnabled ? Visibility.Hidden : Visibility.Visible);
        }

        public override void DisplayKeyHook(bool enabled) {
            txtRcHookOn.Visibility = (enabled ? Visibility.Visible : Visibility.Hidden);
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

        private void HandleCanvasMouseWheel(object sender, MouseWheelEventArgs e) {
            if (!state.controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.SendMouseWheel(e.Delta);
        }

        private void HandleCanvasMouseUp(object sender, MouseButtonEventArgs e) {
            if (!state.controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            //if (rcCanvas.Contains(e.GetPosition((Canvas)sender))) {
            rc.SendMouseUp(e.ChangedButton);

            if (e.ChangedButton == MouseButton.Left)
                state.mouseHeldLeft = false;
            if (e.ChangedButton == MouseButton.Right)
                state.mouseHeldRight = false;

            state.Window.DebugKeyboard();
            //}
        }

        private void HandleCanvasMouseDown(object sender, MouseButtonEventArgs e) {
            if (state.connectionStatus != ConnectionStatus.Connected)
                return;

            if (state.useMultiScreen) {
                System.Windows.Point point = e.GetPosition(rcCanvas);
                RCScreen screenPointingTo = state.GetScreenUsingMouse(canvasOffsetX + (int)point.X, canvasOffsetY + (int)point.Y);
                if (screenPointingTo == null)
                    return;

                if (state.controlEnabled) {
                    if (state.virtualViewWant != state.virtualCanvas && screenPointingTo != state.CurrentScreen) {
                        state.Window.FromGlChangeScreen(screenPointingTo, true);
                        return;
                    }
                } else {
                    if (e.ClickCount == 2) {
                        state.Window.SetControlEnabled(true);
                    } else if (e.ChangedButton == MouseButton.Left) {
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
                    if (e.ClickCount == 2)
                        state.Window.SetControlEnabled(true);

                    return;
                }
            }

            if (e.ChangedButton == MouseButton.Middle) {
                if (e.ClickCount == 1) //Logitech bug
                    state.Window.PerformAutotype();
            } else {
                if (state.windowActivatedMouseMove)
                    HandleCanvasMouseMove(sender, e);

                rc.SendMouseDown(e.ChangedButton);

                if (e.ChangedButton == MouseButton.Left)
                    state.mouseHeldLeft = true;
                if (e.ChangedButton == MouseButton.Right)
                    state.mouseHeldRight = true;

                state.Window.DebugKeyboard();
            }
        }

        private void HandleCanvasMouseMove(object sender, MouseEventArgs e) {
            if (state.CurrentScreen == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            state.windowActivatedMouseMove = false;

            if (state.useMultiScreen) {
                System.Windows.Point point = e.GetPosition(rcCanvas);
                point.X += canvasOffsetX;
                point.Y += canvasOffsetY;
                state.Window.DebugMouseEvent((int)point.X, (int)point.Y);

                RCScreen screenPointingTo = state.GetScreenUsingMouse((int)point.X, (int)point.Y);
                if (screenPointingTo == null)
                    return;

                if (state.virtualViewWant == state.virtualCanvas && state.CurrentScreen.screen_id != screenPointingTo.screen_id) {
                    //We are in overview, change which screen gets texture updates
                    state.Window.FromGlChangeScreen(screenPointingTo, false);
                }

                if (!state.controlEnabled || !state.WindowIsActive())
                    return;

                rc.SendMousePosition((int)point.X, (int)point.Y);
            } else {
                //Legacy behavior
                if (!state.controlEnabled || !state.WindowIsActive())
                    return;

                //throw new NotImplementedException();
                /*
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
                */
            }

            /*
            if (!controlEnabled || connectionStatus != ConnectionStatus.Connected)
                return;

            System.Windows.Point point = e.GetPosition((Canvas)sender);
            DebugMouseEvent((int)point.X, (int)point.Y);

            rc.SendMousePosition(canvasOffsetX + (int)point.X, canvasOffsetY + (int)point.Y);
            */
        }

        public override void CameraToCurrentScreen() {
            SetCanvas(state.CurrentScreen.rect.X, state.CurrentScreen.rect.Y, state.CurrentScreen.rect.Width, state.CurrentScreen.rect.Height);
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

            SetCanvas(lowestX, lowestY, Math.Abs(lowestX) + highestX, Math.Abs(lowestY) + highestY);

            state.virtualViewWant = state.virtualCanvas;
            state.virtualRequireViewportUpdate = true;
        }

        public override bool SwitchToMultiScreen() {
            return true;
        }

        public override bool SwitchToLegacy() {
            return false;
        }

        public override void UpdateScreenLayout(int lowestX, int lowestY, int highestX, int highestY) {
            canvasOffsetX = lowestX;
            canvasOffsetY = lowestY;

            Dispatcher.Invoke((Action)delegate {
                rcCanvas.Children.Clear();

                rcCanvas.Width = Math.Abs(lowestX) + highestX;
                rcCanvas.Height = Math.Abs(lowestY) + highestY;

                foreach (RCScreen screen in state.ListScreen) {
                    screen.CanvasImage = new System.Windows.Controls.Image {
                        Height = screen.rect.Height,
                        Width = screen.rect.Width
                    };

                    rcCanvas.Children.Add(screen.CanvasImage);
                    Canvas.SetLeft(screen.CanvasImage, screen.rect.X - canvasOffsetX);
                    Canvas.SetTop(screen.CanvasImage, screen.rect.Y - canvasOffsetY);
                }
            });
        }
    }
}