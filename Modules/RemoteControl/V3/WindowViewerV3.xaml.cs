using KLC;
using LibKaseya;
using NTR;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

//To look into regarding the changes to Key/Keys
//https://stackoverflow.com/questions/1153009/how-can-i-convert-system-windows-input-key-to-system-windows-forms-keys
//YUV source:
//https://github.com/minghuam/opentk-gst/blob/master/ThreadedGLSLVideoPlayer.cs

namespace KLC_Finch {

    /// <summary>
    /// Interaction logic for WindowViewer.xaml
    /// </summary>
    public partial class WindowViewerV3 : WindowViewer {
        public Settings Settings;

        private RCv rcv;

        private ScreenStatus screenStatus;
        private bool requiredApproval;
        private bool autotypeAlwaysConfirmed;
        private string clipboard = "";
        private readonly ClipBoardMonitor clipboardMon;
        private TSSession currentTSSession = null;
        private readonly FPSCounter fpsCounter;
        private double fpsLast;

        private bool keyDownWin;
        private readonly List<TSSession> listTSSession = new List<TSSession>();
        private readonly RCScreen legacyScreen;
        //private readonly object lockFrameBuf = new object();

        private readonly Timer timerHealth;
        private readonly Timer timerClipboard;

        private readonly Agent.OSProfile endpointOS;
        private readonly string endpointLastUser;
        private string[] arrAdmins = new string[] { "administrator", "brandadmin", "adminc", "company" };

        private readonly WindowScreens winScreens;
        private readonly KeyboardHook keyHook;
        private bool ssKeyHookAllow;
        private bool ssClipboardSync;

        public WindowViewerV3(IRemoteControl rc, int virtualWidth = 1920, int virtualHeight = 1080, Agent.OSProfile endpointOS = Agent.OSProfile.Other, string endpointLastUser = "") {
            InitializeComponent();

            this.rc = rc;
            state = new RCstate(this);

            winScreens = new WindowScreens();
            keyHook = new KeyboardHook();
            keyHook.KeyDown += KeyHook_KeyDown;
            keyHook.KeyUp += KeyHook_KeyUp;
            toolVersion.Header = "Build date: " + App.Version;

            this.endpointOS = endpointOS;
            this.endpointLastUser = endpointLastUser;

            Settings = App.Settings;
            switch (App.Settings.GraphicsModeV3) {
                case GraphicsMode.OpenGL_YUV:
                case GraphicsMode.OpenGL_RGB:
                    rcv = new RCvOpenGL(rc, state);
                    break;
                case GraphicsMode.OpenGL_WPF_YUV:
                case GraphicsMode.OpenGL_WPF_RGB:
                    rcv = new RCvOpenGLWPF(rc, state);
                    break;
                case GraphicsMode.Canvas_RGB:
                case GraphicsMode.Canvas_Y:
                    rcv = new RCvCanvas(rc, state);
                    break;
            }
            placeholder.Child = rcv;

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

            SetControlEnabled(false, false); //Just for the visual

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

            WindowUtilities.ActivateWindow(this);
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
                state.useMultiScreen = Settings.StartMultiScreen;

                ////Fix for Canvas
                //toolScreenMode.IsEnabled = glSupported;
                //if (!glSupported)
                //state.useMultiScreen = true;

                if (state.useMultiScreen) {
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

            if (!state.controlEnabled || !ssKeyHookAllow) {
                if (keyHook.IsActive)
                    keyHook.Uninstall();
            } else {
                if (!keyHook.IsActive)
                    keyHook.Install();
            }

            if (canCheckKeyboard) //More like canUpdateUI
                rcv.DisplayKeyHook(keyHook.IsActive && Settings.DisplayOverlayKeyboardHook);
        }

        public override void ClearApproval() {
            rcv.DisplayApproval(false);
        }

        public override void LoadCursor(int cursorX, int cursorY, int cursorWidth, int cursorHeight, int cursorHotspotX, int cursorHotspotY, byte[] remaining) {
            if (state.textureCursor != null)
                state.textureCursor.Load(new Rectangle(cursorX, cursorY, cursorWidth, cursorHeight), remaining);
        }

        public override void LoadTexture(int width, int height, Bitmap decomp) {
            if (screenStatus == ScreenStatus.Preparing)
                return;

            if (state.useMultiScreen) {

                #region Multi-Screen

                RCScreen scr = null;
                if (state.CurrentScreen != null && state.CurrentScreen.rect.Width == width && state.CurrentScreen.rect.Height == height)
                    scr = state.CurrentScreen;
                else if (state.CurrentScreen != null && state.CurrentScreen.rectFixed.Width == width && state.CurrentScreen.rectFixed.Height == height)
                    scr = state.CurrentScreen;
                else if (state.previousScreen != null && state.previousScreen.rect.Width == width && state.previousScreen.rect.Height == height)
                    scr = state.previousScreen;
                else if (state.previousScreen != null && state.previousScreen.rectFixed.Width == width && state.previousScreen.rectFixed.Height == height)
                    scr = state.previousScreen;
                else {
                    List<RCScreen> scrMatch = state.ListScreen.FindAll(x => x.rect.Width == width && x.rect.Height == height);
                    List<RCScreen> scrMatchFixed = state.ListScreen.FindAll(x => x.rectFixed.Width == width && x.rectFixed.Height == height);
                    List<RCScreen> scrMatchHalf = state.ListScreen.FindAll(x => x.rectFixed.Width == width / 2 && x.rectFixed.Height == height / 2);
                    if (scrMatch.Count == 1) {
                        scr = scrMatch[0];
                    } else if (scrMatchFixed.Count == 1) {
                        scr = scrMatchFixed[0];
                    } else if (scrMatchHalf.Count == 1) {
                        //Console.WriteLine("Mac with Retina display?");
                        scr = scrMatchHalf[0];
                        state.legacyVirtualWidth = scr.rectFixed.Width = width;
                        state.legacyVirtualHeight = scr.rectFixed.Height = height;
                        rcv.CameraToCurrentScreen();
                    } else {
                        //Console.WriteLine("Forced switch from Multi-Screen to Legacy");
                        if (screenStatus == ScreenStatus.Stable) {
                            if (SwitchToLegacyRendering())
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

                if (state.legacyVirtualWidth != width || state.legacyVirtualHeight != height) {
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

                if (state.textureLegacy != null)
                    state.textureLegacy.Load(new Rectangle(0, 0, width, height), decomp);
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

            rcv.Refresh();
        }

        public override void LoadTextureRaw(byte[] buffer, int width, int height, int stride) {
            if (screenStatus == ScreenStatus.Preparing || width * height <= 0)
                return;

            //lock (lockFrameBuf) {
            if (state.useMultiScreen) {

                #region Multi-Screen

                RCScreen scr = null;
                if (state.CurrentScreen != null && state.CurrentScreen.rect.Width == width && state.CurrentScreen.rect.Height == height)
                    scr = state.CurrentScreen;
                else if (state.CurrentScreen != null && state.CurrentScreen.rectFixed.Width == width && state.CurrentScreen.rectFixed.Height == height)
                    scr = state.CurrentScreen;
                else if (state.previousScreen != null && state.previousScreen.rect.Width == width && state.previousScreen.rect.Height == height)
                    scr = state.previousScreen;
                else if (state.previousScreen != null && state.previousScreen.rectFixed.Width == width && state.previousScreen.rectFixed.Height == height)
                    scr = state.previousScreen;
                else {
                    List<RCScreen> scrMatch = state.ListScreen.FindAll(x => x.rect.Width == width && x.rect.Height == height);
                    List<RCScreen> scrMatchFixed = state.ListScreen.FindAll(x => x.rectFixed.Width == width && x.rectFixed.Height == height);
                    List<RCScreen> scrMatchHalf = state.ListScreen.FindAll(x => x.rectFixed.Width == width / 2 && x.rectFixed.Height == height / 2);
                    if (scrMatch.Count == 1) {
                        scr = scrMatch[0];
                    } else if (scrMatchFixed.Count == 1) {
                        scr = scrMatchFixed[0];
                    } else if (scrMatchHalf.Count == 1) {
                        //Console.WriteLine("Mac with Retina display?");
                        scr = scrMatchHalf[0];
                        state.legacyVirtualWidth = scr.rectFixed.Width = width;
                        state.legacyVirtualHeight = scr.rectFixed.Height = height;
                        rcv.CameraToCurrentScreen();
                    } else {
                        if (screenStatus == ScreenStatus.Stable) {
                            //Console.WriteLine("Forced switch from Multi-Screen to Legacy");
                            if (SwitchToLegacyRendering())
                                LoadTextureRaw(buffer, width, height, stride);
                            else
                                UpdateScreenLayoutHack();
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

                if (state.legacyVirtualWidth != width || state.legacyVirtualHeight != height) {
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

                state.textureLegacy.LoadRaw(new Rectangle(0, 0, width, height), buffer, stride);

                state.socketAlive = true;

                #endregion Legacy
            }
            //}

            fpsLast = fpsCounter.GetFPS();
            screenStatus = ScreenStatus.Stable;

            rcv.Refresh();
        }

        public override void NotifySocketClosed(string sessionId) {
            if (sessionId == "/control/agent") {
            } else if (state.sessionId != sessionId)
                return;

            state.socketAlive = false;
            state.connectionStatus = ConnectionStatus.Disconnected;

            //rc = null; //Can result in exceptions if a Send event results in a disconnection. Also stops Soft Reconnect is some situations.
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
            switch (rcNotify) {
                case Enums.NotifyApproval.ApproveAllowIfNoUser:
                case Enums.NotifyApproval.ApproveDenyIfNoUser:
                    requiredApproval = true;
                    rcv.DisplayApproval(true);
                    toolReconnect.Header = "Reconnect (reapproval required)";
                    break;

                default:
                    rcv.DisplayApproval(false);
                    break;
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

        public override void SetControlEnabled(bool value, bool isStart = false) {
            if (isStart) {
                state.controlEnabled = Settings.StartControlEnabled;
                if (Settings.StartMultiScreen && state.controlEnabled)
                    rcv.CameraToCurrentScreen();
                else
                    state.useMultiScreenOverview = true;
            } else
                state.controlEnabled = value;

            Dispatcher.Invoke((Action)delegate {
                if (state.controlEnabled)
                    toolToggleControl.Content = "Control Enabled";
                else
                    toolToggleControl.Content = "Control Disabled";
                toolToggleControl.FontWeight = (state.controlEnabled ? FontWeights.Normal : FontWeights.Bold);

                toolSendCtrlAltDel.IsEnabled = state.controlEnabled;

                if (state.connectionStatus != ConnectionStatus.Disconnected) {
                    rcv.DisplayControl(value);
                }

                KeyHookSet(true);
            });
        }

        public override void SetSessionID(string sessionId) {
            state.sessionId = sessionId;

            if (sessionId != null) {
                state.socketAlive = true;
                //connectionStatus = ConnectionStatus.Connected;
            }
        }

        public override void SetTitle(string title, bool modePrivate) {
            this.Title = state.BaseTitle = title + "::" + (modePrivate ? "Private" : "Shared");
            if (modePrivate)
                toolReconnect.Header = "Reconnect (lose private session)";
        }

        public void SetVirtual(int virtualX, int virtualY, int virtualWidth, int virtualHeight) {
            if (state.useMultiScreen) {
                state.virtualViewWant = new Rectangle(virtualX, virtualY, virtualWidth, virtualHeight);
            } else {
                state.legacyVirtualWidth = virtualWidth;
                state.legacyVirtualHeight = virtualHeight;
                state.virtualCanvas = state.virtualViewWant = new Rectangle(0, 0, virtualWidth, virtualHeight);
            }

            state.virtualRequireViewportUpdate = true;
        }

        public override void UpdateLatency(long ms) {
            state.lastLatency = ms;

            //Dipatcher.Invoke((Action)delegate {
            //toolLatency.Content = string.Format("{0} ms", ms);
            //});
        }

        private void CheckHealth(object sender, ElapsedEventArgs e) {
            /*
            if (powerSaving)
                return;

            Dispatcher.Invoke((Action)delegate {
#if DEBUG
                if (keyHook.IsActive && !IsActive) {
                    MessageBox.Show("[RC:CheckHealth] Keyhook active but not RC window.");
                }
#endif

                //txtDebugLeft.Visibility = (Settings.DisplayOverlayKeyboardMod || Settings.DisplayOverlayKeyboardOther ? Visibility.Visible : Visibility.Collapsed);
                //txtDebugRight.Visibility = (Settings.DisplayOverlayMouse ? Visibility.Visible : Visibility.Collapsed);

                switch (connectionStatus) {
                    case ConnectionStatus.FirstConnectionAttempt:
                        txtRcFrozen.Visibility = Visibility.Collapsed;
                        txtRcConnecting.Visibility = Visibility.Visible;
                        break;

                    case ConnectionStatus.Connected:
                        txtRcConnecting.Visibility = Visibility.Collapsed;

                        if (fpsCounter.SeemsAlive(5000)) {
                            //toolLatency.FontWeight = FontWeights.Normal;
                            txtRcFrozen.Visibility = Visibility.Collapsed;
                        } else {
                            fpsLast = 0;
                            //toolLatency.Content = string.Format("Frozen? | {0} ms", lastLatency);
                            //toolLatency.FontWeight = FontWeights.Bold;
                            txtRcFrozen.Visibility = Visibility.Visible;
                        }
                        toolLatency.Content = string.Format("FPS: {0} | {1} ms", fpsLast, lastLatency);
                        break;

                    case ConnectionStatus.Disconnected:
                        toolLatency.Content = "N/C";
                        if (App.alternative == null || !App.alternative.socketActive)
                            toolReconnect.Header = "Hard Reconnect Required";
                        txtRcControlOff1.Visibility = txtRcControlOff2.Visibility = txtRcNotify.Visibility = Visibility.Collapsed;
                        txtRcFrozen.Visibility = Visibility.Collapsed;
                        txtRcDisconnected.Visibility = Visibility.Visible;
                        rcBorderBG.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Maroon);

                        if (keyHook.IsActive) {
                            keyHook.Uninstall();
                            txtRcHookOn.Visibility = Visibility.Collapsed;
                        }

                        timerHealth.Stop();
                        break;
                }
            });
            */
        }

        public void PerformAutotype() {
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

        private bool SwitchToLegacyRendering() {
            if (rcv.SwitchToLegacy()) {
                Dispatcher.Invoke((Action)delegate {
                    toolScreenMode.Content = "Legacy";
                    toolScreenOverview.Visibility = Visibility.Collapsed;
                    toolZoomIn.Visibility = Visibility.Collapsed;
                    toolZoomOut.Visibility = Visibility.Collapsed;
                });

                return true;
            }

            return false;
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
            if (!state.controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
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
            if (state.virtualViewWant != state.virtualCanvas) //Not in Overview?
                rcv.CameraToCurrentScreen(); //Multi-Screen Alt Fit
        }

        private void ToolPanicRelease_Click(object sender, RoutedEventArgs e) {
            if (!state.controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
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
            state.useMultiScreenOverview = !state.useMultiScreenOverview;
            if (state.useMultiScreenOverview) {
                SetControlEnabled(false);
                rcv.CameraToOverview();
            } else {
                rcv.CameraToCurrentScreen();
            }
        }

        private void ToolScreenshotToClipboard_Click(object sender, RoutedEventArgs e) {
            if (state.connectionStatus != ConnectionStatus.Connected)
                return;

            rc.CaptureNextScreen();
        }

        private void ToolSendCtrlAltDel_Click(object sender, RoutedEventArgs e) {
            if (!state.controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
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
            SetControlEnabled(!state.controlEnabled);
        }

        private void ToolZoomIn_Click(object sender, RoutedEventArgs e) {
            if (!state.useMultiScreen)
                return;
            if (state.virtualViewWant.Width - 200 < 0 || state.virtualViewWant.Height - 200 < 0)
                return;

            state.virtualViewWant = new Rectangle(state.virtualViewWant.X + 100, state.virtualViewWant.Y + 100, state.virtualViewWant.Width - 200, state.virtualViewWant.Height - 200);
            state.virtualRequireViewportUpdate = true;

            //DebugKeyboard();
        }

        private void ToolZoomOut_Click(object sender, RoutedEventArgs e) {
            if (!state.useMultiScreen)
                return;

            state.virtualViewWant = new Rectangle(state.virtualViewWant.X - 100, state.virtualViewWant.Y - 100, state.virtualViewWant.Width + 200, state.virtualViewWant.Height + 200);
            state.virtualRequireViewportUpdate = true;

            //DebugKeyboard();
        }

        private void Window_Activated(object sender, EventArgs e) {
            if (!state.controlEnabled || state.CurrentScreen == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            KeyHookSet(true);
            state.windowActivatedMouseMove = true;
        }

        private void Window_Closed(object sender, EventArgs e) {
            rcv.ControlUnload();

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
            if (!state.controlEnabled || state.CurrentScreen == null || state.connectionStatus != ConnectionStatus.Connected)
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

            rcv.ControlLoaded(rc, state);
        }

        #region Host Desktop Configuration (Screens of current session)

        public override void UpdateScreenLayout(dynamic json, string jsonstr = "") {
            screenStatus = ScreenStatus.Preparing;

            state.ListScreen.Clear();
            state.previousScreen = state.CurrentScreen = null;

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
                    state.ListScreen.Add(newScreen);

                    //Add to toolbar menu
                    MenuItem item = new MenuItem {
                        Header = newScreen.ToString()// screen_name + ": (" + screen_width + " x " + screen_height + " at " + screen_x + ", " + screen_y + ")";
                    };
                    item.Click += new RoutedEventHandler(ToolScreen_ItemClicked);

                    toolScreen.DropdownMenu.Items.Add(item);

                    //Private and Mac seem to bug out if you change screens, cause there's only one screen
                    toolScreen.Opacity = (state.ListScreen.Count > 1 ? 1.0 : 0.6);

                    if (screen_id == default_screen) {
                        state.CurrentScreen = newScreen;
                        //Legacy
                        state.legacyVirtualHeight = state.CurrentScreen.rect.Height;
                        state.legacyVirtualWidth = state.CurrentScreen.rect.Width;
                        state.virtualRequireViewportUpdate = true;

                        toolScreen.Content = state.CurrentScreen.screen_name;
                        toolScreen.ToolTip = state.CurrentScreen.StringResPos();
                    }
                }
            });

            int lowestX = state.ListScreen.Min(x => x.rectFixed.X);
            int lowestY = state.ListScreen.Min(x => x.rectFixed.Y);
            int highestX = state.ListScreen.Max(x => x.rectFixed.Right);
            int highestY = state.ListScreen.Max(x => x.rectFixed.Bottom);
            rcv.SetCanvas(lowestX, lowestY, highestX, highestY);

            rcv.UpdateScreenLayout(lowestX, lowestY, highestX, highestY);

            rc.UpdateScreens(jsonstr);
            winScreens.UpdateStartScreens(jsonstr);
            winScreens.SetCanvas(lowestX, lowestY, highestX, highestY);

            screenStatus = ScreenStatus.LayoutReady;
        }

        public void FromGlChangeScreen(RCScreen screen, bool moveCamera = true) {
            state.previousScreen = state.CurrentScreen;
            state.CurrentScreen = screen;
            rc.ChangeScreen(state.CurrentScreen.screen_id);

            rcv.CameraFromClickedScreen(screen, moveCamera);

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

            state.previousScreen = state.CurrentScreen;
            state.CurrentScreen = state.ListScreen.First(x => x.screen_name == screen_selected[0]);
            rc.ChangeScreen(state.CurrentScreen.screen_id);

            if (state.useMultiScreen)
                rcv.CameraToCurrentScreen();

            toolScreen.Content = state.CurrentScreen.screen_name;
            toolScreen.ToolTip = state.CurrentScreen.StringResPos();
            foreach (MenuItem item in toolScreen.DropdownMenu.Items) {
                item.IsChecked = (item == source);
            }
        }

        public void SetScreen(string id) {
            state.previousScreen = state.CurrentScreen;
            state.CurrentScreen = state.ListScreen.First(x => x.screen_id == id);
            rc.ChangeScreen(state.CurrentScreen.screen_id);

            if (state.useMultiScreen)
                rcv.CameraToCurrentScreen();

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

            state.useMultiScreen = Settings.StartMultiScreen;

            toolTSSession.Content = currentTSSession.session_name;

            if (state.useMultiScreen) {
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

        public void DebugKeyboard() {
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

                if (state.mouseHeldRight)
                    strKeyboard = "MouseRight" + (strKeyboard == "" ? "" : " | " + strKeyboard);
                if (state.mouseHeldLeft)
                    strKeyboard = "MouseLeft" + (strKeyboard == "" ? "" : " | " + strKeyboard);
            }

            if (Settings.DisplayOverlayKeyboardMod || Settings.DisplayOverlayKeyboardOther) {
                rcv.DisplayDebugKeyboard(strKeyboard);
            }
        }

        public void DebugMouseEvent(int X, int Y) {
            if (Settings.DisplayOverlayMouse)
                rcv.DisplayDebugMouseEvent(X, Y);
        }

        private void ToolScreenMode_Click(object sender, RoutedEventArgs e) {
            state.useMultiScreen = !state.useMultiScreen;
            if (state.useMultiScreen) {
                state.useMultiScreenOverview = false;
                rcv.CameraToCurrentScreen();

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
            if (!state.controlEnabled || state.connectionStatus != ConnectionStatus.Connected)
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
            if (WindowState == System.Windows.WindowState.Minimized) {
                if (Settings.PowerSaveOnMinimize)
                    rcv.ParentStateChange(false);
            } else {
                rcv.ParentStateChange(true);
            }
        }

        private void toolOpenGLInfo_Click(object sender, RoutedEventArgs e) {
            throw new NotImplementedException();
            //MessageBox.Show("Render capability: 0x" + System.Windows.Media.RenderCapability.Tier.ToString("X") + "\r\n\r\nOpenGL Version: " + glVersion, "KLC-Finch: OpenGL Info");
        }

        private void KeyWinSet(bool set) {
            if (!state.controlEnabled || endpointOS == Agent.OSProfile.Mac || state.connectionStatus != ConnectionStatus.Connected)
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
            } else if (!state.controlEnabled) {
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
                rcv.CameraToOverview();
            }

            if (state.connectionStatus != ConnectionStatus.Connected || !state.controlEnabled)
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
    }
}