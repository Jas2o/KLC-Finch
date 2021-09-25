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
            txtZoom.Visibility = Visibility.Collapsed;
        }

        public override bool SupportsLegacy { get { return false; } }
        public override void CameraFromClickedScreen(RCScreen screen, bool moveCamera = true) {
            if (state.useMultiScreen && moveCamera)
                CameraToCurrentScreen();
        }

        public override void CameraToCurrentScreen() {
            if (!state.useMultiScreen || state.CurrentScreen == null)
                return;

            state.useMultiScreenOverview = false;
            state.useMultiScreenPanZoom = false;
            ZoomSlider.Visibility = Visibility.Collapsed;
            rcScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            rcScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;

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

                int virtualX = state.CurrentScreen.rect.X - (adjustLeft ? 80 : 0);
                int virtualY = state.CurrentScreen.rect.Y - (adjustUp ? 80 : 0);

                //SetCanvas(Math.Max(canvasOffsetX, virtualX), Math.Max(canvasOffsetY, virtualY),
                //state.CurrentScreen.rectFixed.Width + (adjustLeft ? 80 : 0) + (adjustRight ? 80 : 0),
                //state.CurrentScreen.rectFixed.Height + (adjustUp ? 80 : 0) + (adjustDown ? 80 : 0));

                SetCanvas(virtualX, virtualY,
                    state.CurrentScreen.rect.Width + (adjustLeft ? 80 : 0) + (adjustRight ? 80 : 0),
                    state.CurrentScreen.rect.Height + (adjustUp ? 80 : 0) + (adjustDown ? 80 : 0));
            } else
                SetCanvas(state.CurrentScreen.rect.X, state.CurrentScreen.rect.Y, state.CurrentScreen.rect.Width, state.CurrentScreen.rect.Height);

            //SetCanvas(state.CurrentScreen.rect.X, state.CurrentScreen.rect.Y, state.CurrentScreen.rect.Width, state.CurrentScreen.rect.Height);
        }

        public override void CameraToOverview() {
            if (!state.useMultiScreen)
                return;

            state.useMultiScreenOverview = true;
            state.useMultiScreenPanZoom = false;
            Dispatcher.Invoke((Action)delegate {
                ZoomSlider.Visibility = Visibility.Collapsed;
                rcScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
                rcScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            });

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

            SetCanvas(lowestX, lowestY, Math.Abs(lowestX) + highestX, Math.Abs(lowestY) + highestY);
        }

        public override void ZoomIn() {
            state.useMultiScreenPanZoom = true;

            ZoomSlider.Visibility = Visibility.Visible;
            rcScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            rcScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;

            ZoomSlider.Value += 0.05;
        }

        public override void ZoomOut() {
            state.useMultiScreenPanZoom = true;

            ZoomSlider.Visibility = Visibility.Visible;
            rcScrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Visible;
            rcScrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Visible;

            ZoomSlider.Value -= 0.05;
        }

        public override void CheckHealth() {
            txtDebugLeft.Visibility = (App.Settings.DisplayOverlayKeyboardMod || App.Settings.DisplayOverlayKeyboardOther ? Visibility.Visible : Visibility.Collapsed);
            txtDebugRight.Visibility = (App.Settings.DisplayOverlayMouse ? Visibility.Visible : Visibility.Collapsed);
            txtZoom.Visibility = (App.Settings.DisplayOverlayPanZoom ? Visibility.Visible : Visibility.Collapsed);

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
                    rcBorderBG.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Maroon);

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

            if (!state.useMultiScreen)
                state.SetVirtual(0, 0, state.virtualWidth, state.virtualHeight);

            //rcCanvas.Children.Clear();
            rcRectangleExample.Visibility = Visibility.Hidden;
            canvasListRectangle = new List<System.Windows.Shapes.Rectangle>();

            if (App.Settings.RendererAlt) {
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

        public override void ControlUnload() {
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
        }

        public override void Refresh() {
            if (state.useMultiScreen) return;

            /*
            Dispatcher.Invoke((Action)delegate {
                //Canvas.SetLeft(state.legacyScreen.CanvasImage, 0);
                //Canvas.SetTop(state.legacyScreen.CanvasImage, 0);

                legacyCanvas.Width = legacyViewbox.ActualWidth;
                legacyCanvas.Height = legacyViewbox.ActualHeight;
            });
            */
        }

        public override void SetCanvas(int virtualX, int virtualY, int virtualWidth, int virtualHeight) { //More like lowX, lowY, highX, highY
            if (rcScrollViewer.ActualHeight == 0 || rcScrollViewer.ViewportHeight == 0)
                return;

            float currentAspectRatio = (float)rcScrollViewer.ViewportWidth / (float)rcScrollViewer.ViewportHeight;
            float targetAspectRatio = (float)virtualWidth / (float)virtualHeight;
            int width = virtualWidth;
            int height = virtualHeight;

            if (currentAspectRatio > targetAspectRatio) {
                //Pillarbox
                width = (int)((float)height * currentAspectRatio);
                //vpX = (width - state.virtualViewWant.Width) / 2;
            } else {
                //Letterbox
                height = (int)((float)width / currentAspectRatio);
                //vpY = (height - state.virtualViewWant.Height) / 2;
            }

            double scaleX = (double)rcScrollViewer.ViewportWidth / (double)width;
            double scaleY = (double)rcScrollViewer.ViewportHeight / (double)height;

            Dispatcher.Invoke((Action)delegate {
                ZoomSlider.Value = scaleY;

                rcScrollViewer.ScrollToHorizontalOffset((virtualX - canvasOffsetX) * ZoomSlider.Value);
                rcScrollViewer.ScrollToVerticalOffset((virtualY - canvasOffsetY) * ZoomSlider.Value);
            });
        }
        public override bool SwitchToLegacy() {
            state.useMultiScreen = true;

            return false;
        }

        public override bool SwitchToMultiScreen() {
            state.useMultiScreen = true;

            //dockCanvas.Visibility = Visibility.Visible;
            //legacyBorderBG.Visibility = Visibility.Hidden;

            return true;
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

                    screen.SetCanvasFilled();
                }

                //ZoomSlider.Minimum = rcScrollViewer.ScrollableHeight / virtualHeight;

                //--

                state.legacyScreen.CanvasImage = new System.Windows.Controls.Image();
                state.legacyScreen.SetCanvasFilled();

                //legacyCanvas.Children.Clear();
                //legacyCanvas.Children.Add(state.legacyScreen.CanvasImage);
            });

            if (state.useMultiScreen) {
                if (!App.Settings.StartControlEnabled)
                    CameraToOverview();
                else
                    CameraToCurrentScreen();
            }
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
                    if (!state.useMultiScreenPanZoom &&  screenPointingTo != state.CurrentScreen) {
                        state.Window.FromGlChangeScreen(screenPointingTo, true);
                        return;
                    }
                } else {
                    if (e.ClickCount == 2) {
                        state.Window.SetControlEnabled(true);
                    } else if (e.ChangedButton == MouseButton.Left) {
                        if (!state.useMultiScreenPanZoom) {
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

                if (!state.controlEnabled) {
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

                //Console.WriteLine(string.Format("{0},{1} of {2},{3}", point.X, point.Y, screenPointingTo.rectFixed.X, screenPointingTo.rectFixed.Y));
                //Console.WriteLine(state.CurrentScreen.screen_id + " != " + screenPointingTo.screen_id);

                if ((state.useMultiScreenOverview || state.useMultiScreenPanZoom) && state.CurrentScreen.screen_id != screenPointingTo.screen_id) {
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

        private void HandleCanvasMouseWheel(object sender, MouseWheelEventArgs e) {
            if (!state.controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.SendMouseWheel(e.Delta);
        }
        private void rcScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e) {
            e.Handled = true;

            HandleCanvasMouseWheel(sender, e);
        }

        private void rcScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e) {
            if(state.useMultiScreen && !state.useMultiScreenPanZoom) {
                if(state.useMultiScreenOverview)
                    CameraToOverview();
                else
                    CameraToCurrentScreen();
            }
        }
    }
}