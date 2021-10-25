using KLC;
using LibKaseya;
using NTR;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    public partial class WindowViewerV3 : Window {
        public Settings Settings;

        private readonly ClipBoardMonitor clipboardMon;
        private readonly string endpointLastUser;
        private readonly Agent.OSProfile endpointOS;
        private readonly FPSCounter fpsCounter;
        private readonly KeycodeV3 keyctrl = KeycodeV3.Dictionary[System.Windows.Forms.Keys.ControlKey];
        private readonly KeyboardHook keyHook;
        private readonly KeycodeV3 keyshift = KeycodeV3.Dictionary[System.Windows.Forms.Keys.ShiftKey];
        private readonly KeycodeV3 keywin = KeycodeV3.Dictionary[System.Windows.Forms.Keys.LWin];
        private readonly List<KeycodeV3> listHeldKeysMod = new List<KeycodeV3>();
        private readonly List<KeycodeV3> listHeldKeysOther = new List<KeycodeV3>();
        private readonly List<TSSession> listTSSession = new List<TSSession>();
        private readonly Timer timerClipboard;
        private readonly Timer timerHealth;
        private readonly WindowScreens winScreens;
        private string[] arrAdmins = new string[] { "administrator", "brandadmin", "adminc", "company" };
        private bool autotypeAlwaysConfirmed;
        private string clipboard = "";
        private TSSession currentTSSession = null;
        private double fpsLast;
        private bool keyDownWin;
        private Progress<int> progress;
        private ProgressDialog progressDialog;
        private int progressValue;
        private IRemoteControl rc;
        private RCv rcv;
        private bool requiredApproval;
        private ScreenStatus screenStatus;
        private bool ssKeyHookAllow;
        private RCstate state;

        public WindowViewerV3(int renderer, IRemoteControl rc, Agent.OSProfile endpointOS = Agent.OSProfile.Other, string endpointLastUser = "") {
            InitializeComponent();

            this.rc = rc;
            state = new RCstate(this);
            this.DataContext = state;

            winScreens = new WindowScreens(endpointOS);
            keyHook = new KeyboardHook();
            keyHook.KeyDown += KeyHook_KeyDown;
            keyHook.KeyUp += KeyHook_KeyUp;
            toolVersion.Header = "Build date: " + App.Version;

            this.endpointOS = endpointOS;
            this.endpointLastUser = endpointLastUser;

            Settings = App.Settings;
            switch (renderer) {
                case 0:
                    rcv = new RCvOpenGL(rc, state);
                    break;

                case 1:
                    rcv = new RCvOpenGLWPF(rc, state);
                    break;

                case 2:
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

            SetControlEnabled(false, true); //Just for the visual

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

            state.legacyScreen = new RCScreen("Legacy", "Legacy", 800, 600, 0, 0);

            WindowUtilities.ActivateWindow(this);
        }

        public void AddTSSession(string session_id, string session_name) {
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

        public void ClearApproval() {
            rcv.DisplayApproval(false);
        }

        public void ClearTSSessions() {
            listTSSession.Clear();
            currentTSSession = null;

            Dispatcher.Invoke((Action)delegate {
                toolTSSession.DropdownMenu.Items.Clear();
            });
        }

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

        public void UpdateScreenLayoutReflow() {
            int lowestX = state.ListScreen.Min(x => x.rect.X);
            int lowestY = state.ListScreen.Min(x => x.rect.Y);
            int highestX = state.ListScreen.Max(x => x.rect.Right);
            int highestY = state.ListScreen.Max(x => x.rect.Bottom);
            rcv.SetCanvas(lowestX, lowestY, highestX, highestY);
            rcv.UpdateScreenLayout(lowestX, lowestY, highestX, highestY);
            winScreens.SetCanvas(lowestX, lowestY, highestX, highestY);
        }

        public void DebugMouseEvent(int X, int Y) {
            if (Settings.DisplayOverlayMouse)
                rcv.DisplayDebugMouseEvent(X, Y);
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

        public RCScreen GetCurrentScreen() {
            return state.CurrentScreen;
        }

        public List<RCScreen> GetListScreen() {
            return state.ListScreen;
        }

        public bool GetStatePowerSaving() {
            return state.powerSaving;
        }

        public void LoadCursor(int cursorX, int cursorY, int cursorWidth, int cursorHeight, int cursorHotspotX, int cursorHotspotY, byte[] remaining) {
            if (state.textureCursor != null)
                state.textureCursor.Load(new Rectangle(cursorX - cursorHotspotX, cursorY - cursorHotspotY, cursorWidth, cursorHeight), remaining);
        }

        public void LoadTexture(int width, int height, Bitmap decomp) {
            if (screenStatus == ScreenStatus.Preparing)
                return;

            if (state.UseMultiScreen) {
                if (state.CurrentScreen == null) {
                    //Console.WriteLine("[LoadTexture] No matching RCScreen for screen ID: " + screenID);
                    //listScreen might be empty
                    return;
                }

                if (state.CurrentScreen.Texture != null)
                    state.CurrentScreen.Texture.Load(state.CurrentScreen.rect, decomp);
                else {
                    Dispatcher.Invoke((Action)delegate {
                        if (state.CurrentScreen.CanvasImage == null)
                            state.CurrentScreen.CanvasImage = new System.Windows.Controls.Image();
                        state.CurrentScreen.CanvasImage.Width = state.CurrentScreen.rect.Width;
                        state.CurrentScreen.CanvasImage.Height = state.CurrentScreen.rect.Height;

                        state.CurrentScreen.SetCanvasImage(decomp);
                    });
                }
            } else {
                //Legacy
                if (state.CurrentScreen == null)
                    return;

                if (state.legacyVirtualWidth != width || state.legacyVirtualHeight != height) {
                    //Console.WriteLine("[LoadTexture:Legacy] Virtual resolution did not match texture received.");
                    SetVirtual(0, 0, width, height);

                    /*
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
                    */
                }

                if (state.textureLegacy != null)
                    state.textureLegacy.Load(new Rectangle(0, 0, width, height), decomp);
                else {
                    state.legacyScreen.rect = new Rectangle(0, 0, width, height);

                    Dispatcher.Invoke((Action)delegate {
                        if (state.legacyScreen.CanvasImage == null)
                            state.legacyScreen.CanvasImage = new System.Windows.Controls.Image();
                        state.CurrentScreen.CanvasImage.Width = state.CurrentScreen.rect.Width;
                        state.CurrentScreen.CanvasImage.Height = state.CurrentScreen.rect.Height;

                        state.legacyScreen.SetCanvasImage(decomp);
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
            }

            state.UseMultiScreenFixAvailable = (state.CurrentScreen.rect.Width != width);
            if (state.UseMultiScreenFixAvailable) {
                state.legacyVirtualWidth = width;
                state.legacyVirtualHeight = height;

                if (state.previousScreen == null)
                    ToolScreenFix_Click(null, null);
            }
            state.socketAlive = true;

            fpsLast = fpsCounter.GetFPS();
            screenStatus = ScreenStatus.Stable;

            rcv.Refresh();
        }

        public void LoadTextureRaw(byte[] buffer, int width, int height, int stride) {
            if (screenStatus == ScreenStatus.Preparing || width * height <= 0)
                return;

            if (state.UseMultiScreen) {
                if (state.CurrentScreen == null) {
                    //Console.WriteLine("[LoadTexture] No matching RCScreen for screen ID: " + screenID);
                    //listScreen might be empty
                    return;
                }

                if (state.CurrentScreen.Texture != null) {
                    state.CurrentScreen.Texture.LoadRaw(state.CurrentScreen.rect, width, height, stride, buffer);
                } else {
                    //Canvas
                    Dispatcher.Invoke((Action)delegate {
                        if (state.CurrentScreen.CanvasImage == null)
                            state.CurrentScreen.CanvasImage = new System.Windows.Controls.Image();
                        state.CurrentScreen.CanvasImage.Width = width;
                        state.CurrentScreen.CanvasImage.Height = height;

                        state.CurrentScreen.SetCanvasImageBW(width, height, stride, buffer);
                    });
                }
            } else {
                //Legacy
                if (state.CurrentScreen == null)
                    return;

                if (state.legacyVirtualWidth != width || state.legacyVirtualHeight != height) {
                    //Console.WriteLine("[LoadTexture:Legacy] Virtual resolution did not match texture received.");
                    SetVirtual(0, 0, width, height);

                    /*
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
                    */
                }

                state.textureLegacy.LoadRaw(new Rectangle(0, 0, state.CurrentScreen.rect.Width, state.CurrentScreen.rect.Height), width, height, stride, buffer);
            }

            state.UseMultiScreenFixAvailable = (state.CurrentScreen.rect.Width != width);
            if (state.UseMultiScreenFixAvailable) {
                state.legacyVirtualWidth = width;
                state.legacyVirtualHeight = height;

                if (state.previousScreen == null)
                    ToolScreenFix_Click(null, null);
            }
            state.socketAlive = true;

            fpsLast = fpsCounter.GetFPS();
            screenStatus = ScreenStatus.Stable;

            rcv.Refresh();
        }

        public void NotifySocketClosed(string sessionId) {
            if (sessionId == "/control/agent") {
            } else if (state.sessionId != sessionId)
                return;

            state.socketAlive = false;
            state.connectionStatus = ConnectionStatus.Disconnected;

            //rc = null; //Can result in exceptions if a Send event results in a disconnection. Also stops Soft Reconnect is some situations.
        }

        /// <summary>
        /// Now uses Kaseya's Paste Clipboard, unless the Autotype toolbar button is used.
        /// </summary>
        public void PerformAutotype(bool fromToolbar=false) {
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
                    foreach (KeycodeV3 k in listHeldKeysMod)
                        rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                    listHeldKeysMod.Clear();

                    //if(fromToolbar)
                        rc.SendAutotype(text);
                    //else
                        //rc.SendPasteClipboard(text);
                }
            } else {
                //Console.WriteLine("Autotype blocked: too long or had a new line character");
            }
        }

        public void ReceiveClipboard(string content) {
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

        public void SetApprovalAndSpecialNote(Enums.NotifyApproval rcNotify, int machineShowToolTip, string machineNote, string machineNoteLink) {
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

        public void SetControlEnabled(bool value, bool isStart = false) {
            if (isStart) {
                state.ControlEnabled = Settings.StartControlEnabled;
                if (Settings.StartMultiScreen && state.ControlEnabled)
                    rcv.CameraToCurrentScreen();
                else
                    state.UseMultiScreenOverview = true;
            } else
                state.ControlEnabled = value;

            Dispatcher.Invoke((Action)delegate {
                if (state.connectionStatus != ConnectionStatus.Disconnected) {
                    rcv.DisplayControl(value);
                }

                KeyHookSet(true);
            });
        }

        public void SetScreen(string id) {
            state.previousScreen = state.CurrentScreen;
            state.CurrentScreen = state.ListScreen.First(x => x.screen_id == id);
            rc.ChangeScreen(state.CurrentScreen.screen_id);

            if (state.UseMultiScreen)
                rcv.CameraToCurrentScreen();

            Dispatcher.Invoke((Action)delegate {
                toolScreen.Content = state.CurrentScreen.screen_name;
                toolScreen.ToolTip = state.CurrentScreen.StringResPos();

                foreach (MenuItem item in toolScreen.DropdownMenu.Items) {
                    item.IsChecked = (item.Header.ToString() == state.CurrentScreen.ToString());
                }
            });
        }

        public void SetSessionID(string sessionId) {
            state.sessionId = sessionId;

            if (sessionId != null) {
                state.socketAlive = true;
                //connectionStatus = ConnectionStatus.Connected;
            }
        }

        public void SetTitle(string title, bool modePrivate) {
            this.Title = state.BaseTitle = title + "::" + (modePrivate ? "Private" : "Shared");
            if (modePrivate)
                toolReconnect.Header = "Reconnect (lose private session)";
        }

        public void SetVirtual(int virtualX, int virtualY, int virtualWidth, int virtualHeight) {
            if (state.UseMultiScreen) {
                state.virtualViewWant = new Rectangle(virtualX, virtualY, virtualWidth, virtualHeight);
            } else {
                state.legacyVirtualWidth = virtualWidth;
                state.legacyVirtualHeight = virtualHeight;
                state.virtualCanvas = state.virtualViewWant = new Rectangle(0, 0, virtualWidth, virtualHeight);
            }

            state.virtualRequireViewportUpdate = true;
        }

        public void UpdateLatency(long ms) {
            state.lastLatency = ms;

            //Dipatcher.Invoke((Action)delegate {
            //toolLatency.Content = string.Format("{0} ms", ms);
            //});
        }

        public void UpdateScreenLayout(dynamic json, string jsonstr = "") {
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

            int lowestX = state.ListScreen.Min(x => x.rect.X);
            int lowestY = state.ListScreen.Min(x => x.rect.Y);
            int highestX = state.ListScreen.Max(x => x.rect.Right);
            int highestY = state.ListScreen.Max(x => x.rect.Bottom);
            rcv.SetCanvas(lowestX, lowestY, highestX, highestY);

            rcv.UpdateScreenLayout(lowestX, lowestY, highestX, highestY);

            rc.UpdateScreens(jsonstr);
            winScreens.UpdateStartScreens(jsonstr);
            winScreens.SetCanvas(lowestX, lowestY, highestX, highestY);

            screenStatus = ScreenStatus.LayoutReady;
        }

        public void UpdateScreenLayoutHack() {
            if (currentTSSession == null)
                rc.ChangeTSSession("0");
            else
                rc.ChangeTSSession(currentTSSession.session_id);
        }

        private void CheckHealth(object sender, ElapsedEventArgs e) {
            if (state.powerSaving)
                return;

            Dispatcher.Invoke((Action)delegate {
                //if (keyHook.IsActive && !IsActive) { MessageBox.Show("[RC:CheckHealth] Keyhook active but not RC window."); } //For testing
                rcv.CheckHealth();

                //txtDebugLeft.Visibility = (Settings.DisplayOverlayKeyboardMod || Settings.DisplayOverlayKeyboardOther ? Visibility.Visible : Visibility.Collapsed);
                //txtDebugRight.Visibility = (Settings.DisplayOverlayMouse ? Visibility.Visible : Visibility.Collapsed);

                switch (state.connectionStatus) {
                    case ConnectionStatus.FirstConnectionAttempt:
                        //txtRcFrozen.Visibility = Visibility.Collapsed;
                        //txtRcConnecting.Visibility = Visibility.Visible;
                        break;

                    case ConnectionStatus.Connected:
                        //txtRcConnecting.Visibility = Visibility.Collapsed;

                        if (fpsCounter.SeemsAlive(5000)) {
                            toolLatency.FontWeight = FontWeights.Normal;
                            //txtRcFrozen.Visibility = Visibility.Collapsed;
                        } else {
                            fpsLast = 0;
                            toolLatency.Content = string.Format("Frozen? | {0} ms", state.lastLatency);
                            toolLatency.FontWeight = FontWeights.Bold;
                            //txtRcFrozen.Visibility = Visibility.Visible;
                        }
                        toolLatency.Content = string.Format("FPS: {0} | {1} ms", fpsLast, state.lastLatency);
                        break;

                    case ConnectionStatus.Disconnected:
                        toolLatency.Content = "N/C";
                        if (App.alternative == null || !App.alternative.socketActive)
                            toolReconnect.Header = "Hard Reconnect Required";

                        if (keyHook.IsActive)
                            keyHook.Uninstall();

                        timerHealth.Stop();
                        break;
                }
            });
        }

        private void KeyHook_KeyDown(KeyboardHook.VKeys key) {
            Window_PreviewKeyDown2(new System.Windows.Forms.KeyEventArgs((System.Windows.Forms.Keys)key));
        }

        private void KeyHook_KeyUp(KeyboardHook.VKeys key) {
            Window_KeyUp2(new System.Windows.Forms.KeyEventArgs((System.Windows.Forms.Keys)key));
        }

        private void KeyHookSet(bool canCheckKeyboard = false) {
            if (canCheckKeyboard) {
                ssKeyHookAllow = Keyboard.IsKeyToggled(Key.Scroll) || Settings.KeyboardHook;
                if (!IsActive)
                    ssKeyHookAllow = false;
            }

            if (!state.ControlEnabled || !ssKeyHookAllow) {
                if (keyHook.IsActive)
                    keyHook.Uninstall();
            } else {
                if (!keyHook.IsActive)
                    keyHook.Install();
            }

            if (canCheckKeyboard) //More like canUpdateUI
                rcv.DisplayKeyHook(keyHook.IsActive && Settings.DisplayOverlayKeyboardHook);
        }

        private void KeyWinSet(bool set) {
            if (!state.ControlEnabled || endpointOS == Agent.OSProfile.Mac || state.connectionStatus != ConnectionStatus.Connected)
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

        private void LoadSettings(bool isStart = false) {
            if (isStart) {
                if (rcv.SupportsLegacy) {
                    if (endpointOS == Agent.OSProfile.Mac && Settings.StartMultiScreenExceptMac)
                        state.UseMultiScreen = false;
                    else
                        state.UseMultiScreen = Settings.StartMultiScreen;
                } else {
                    state.UseMultiScreen = true;
                }

                //SetControlEnabled(Settings.StartControlEnabled, true);
            }

            autotypeAlwaysConfirmed = Settings.AutotypeSkipLengthCheck;
            ssKeyHookAllow = Settings.KeyboardHook;
            KeyHookSet(false);

            if (Settings.ClipboardSync == 2 && (endpointOS == Agent.OSProfile.Server || arrAdmins.Contains(endpointLastUser.ToLower()))) {
                //Server/Admin only
                state.SsClipboardSync = true;
            } else {
                state.SsClipboardSync = (Settings.ClipboardSync == 1);
            }
        }

        private void ProgressDialog_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e) {
            while (progressValue < 100) {
                progressDialog.ReportProgress(progressValue);
                System.Threading.Thread.Sleep(100);
            }
        }

        private bool SwitchToLegacyRendering() {
            if (rcv.SwitchToLegacy()) {
                return true;
            }

            return false;
        }

        private bool SwitchToMultiScreenRendering() {
            if (rcv.SwitchToMultiScreen()) {
                state.UseMultiScreenOverview = false;
                rcv.CameraToCurrentScreen();

                return true;
            }

            return false;
        }

        private void SyncClipboard(object sender, EventArgs e) {
            try {
                if (state.SsClipboardSync && state.ControlEnabled) {
                    string temp = clipboard;
                    this.ToolClipboardSend_Click(sender, e);
                    if (clipboard != temp) {
                        toolClipboardSend.Background = System.Windows.Media.Brushes.Orange;
                        timerClipboard.Start();
                    }
                    //Console.WriteLine("[Clipboard sync] Success?");
                }
            } catch (Exception) {
                //Console.WriteLine("[Clipboard sync] Fail");
            }
        }

        private void TimerClipboard_Elapsed(object sender, ElapsedEventArgs e) {
            Dispatcher.Invoke((Action)delegate {
                toolClipboardSend.Background = System.Windows.Media.Brushes.Transparent;
                toolClipboardGet.Background = System.Windows.Media.Brushes.Transparent;
            });
            timerClipboard.Stop();
        }

        private void ToolBlockMouseKB_Click(object sender, RoutedEventArgs e) {
            toolBlockMouseKB.IsChecked = !toolBlockMouseKB.IsChecked;
            rc.SendBlackScreenBlockInput(toolBlockScreen.IsChecked, toolBlockMouseKB.IsChecked);
        }

        private void ToolBlockScreen_Click(object sender, RoutedEventArgs e) {
            toolBlockScreen.IsChecked = !toolBlockScreen.IsChecked;
            rc.SendBlackScreenBlockInput(toolBlockScreen.IsChecked, toolBlockMouseKB.IsChecked);
        }

        private void ToolClipboardAutotype_Click(object sender, RoutedEventArgs e) {
            if (!state.ControlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            PerformAutotype(true);
        }

        private void ToolClipboardGet_Click(object sender, RoutedEventArgs e) {
            if (clipboard.Length > 0)
                Clipboard.SetDataObject(clipboard);
        }

        private void ToolClipboardPaste_Click(object sender, RoutedEventArgs e) {
            if (!state.ControlEnabled || state.connectionStatus != ConnectionStatus.Connected)
                return;

            string text = Clipboard.GetText().Trim();
            rc.SendPasteClipboard(text);
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
            state.SsClipboardSync = !state.SsClipboardSync;
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

        private void toolOpenGLInfo_Click(object sender, RoutedEventArgs e) {
            OpenGLSoftwareTest glSoftwareTest = new OpenGLSoftwareTest(50, 50, "OpenGL Test");
            MessageBox.Show("Render capability: 0x" + System.Windows.Media.RenderCapability.Tier.ToString("X") + "\r\n\r\nOpenGL Version: " + glSoftwareTest.Version, "KLC-Finch: OpenGL Info");

            if (!(rcv is RCvCanvas))
                ToolReconnect_Click(sender, e); //Issue with spawning an OpenGL when using GLControl
        }

        private void ToolOptions_Click(object sender, RoutedEventArgs e) {
            WindowOptions winOptions = new WindowOptions(ref Settings, true) {
                Owner = this
            };
            winOptions.ShowDialog();
            LoadSettings();
            if (!state.UseMultiScreenOverview) //Not in Overview?
                rcv.CameraToCurrentScreen(); //Multi-Screen Alt Fit
        }

        private void ToolPanicRelease_Click(object sender, RoutedEventArgs e) {
            if (!state.ControlEnabled || state.connectionStatus != ConnectionStatus.Connected)
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

        private void ToolScreen_ItemClicked(object sender, RoutedEventArgs e) {
            MenuItem source = (MenuItem)e.Source;
            string[] screen_selected = source.Header.ToString().Split(':');

            state.previousScreen = state.CurrentScreen;
            state.CurrentScreen = state.ListScreen.First(x => x.screen_name == screen_selected[0]);
            rc.ChangeScreen(state.CurrentScreen.screen_id);

            if (state.UseMultiScreen)
                rcv.CameraToCurrentScreen();

            toolScreen.Content = state.CurrentScreen.screen_name;
            toolScreen.ToolTip = state.CurrentScreen.StringResPos();
            foreach (MenuItem item in toolScreen.DropdownMenu.Items) {
                item.IsChecked = (item == source);
            }
        }

        private void ToolScreenMode_Click(object sender, RoutedEventArgs e) {
            if (state.UseMultiScreen) {
                SwitchToLegacyRendering();
            } else {
                SwitchToMultiScreenRendering();
            }
        }

        private void ToolScreenOverview_Click(object sender, RoutedEventArgs e) {
            state.UseMultiScreenOverview = !state.UseMultiScreenOverview;
            if (state.UseMultiScreenOverview) {
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
            if (!state.ControlEnabled || state.connectionStatus != ConnectionStatus.Connected)
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

        private void ToolShowMouse_Click(object sender, RoutedEventArgs e) {
            toolShowMouse.IsChecked = !toolShowMouse.IsChecked;
            rc.ShowCursor(toolShowMouse.IsChecked);
        }

        private void ToolToggleControl_Click(object sender, RoutedEventArgs e) {
            SetControlEnabled(!state.ControlEnabled);
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

            state.UseMultiScreen = Settings.StartMultiScreen;

            toolTSSession.Content = currentTSSession.session_name;

            foreach (MenuItem item in toolTSSession.DropdownMenu.Items) {
                item.IsChecked = (item == source);
            }
        }

        private void ToolUpdateScreenLayout_Click(object sender, RoutedEventArgs e) {
            UpdateScreenLayoutHack();
        }

        private void toolViewRCLogs_Click(object sender, RoutedEventArgs e) {
            string logs = App.alternative.session.agent.GetAgentRemoteControlLogs();
            MessageBox.Show(logs, "KLC-Finch: Remote Control Logs");
        }

        private void toolPanZoom_Click(object sender, RoutedEventArgs e) {
            rcv.TogglePanZoom();
        }

        /*
        private void ToolZoomIn_Click(object sender, RoutedEventArgs e) {
            rcv.ZoomIn();
            //DebugKeyboard();
            rcv.Refresh();
        }

        private void ToolZoomOut_Click(object sender, RoutedEventArgs e) {
            rcv.ZoomOut();
            //DebugKeyboard();
            rcv.Refresh();
        }
        */

        private void Window_Activated(object sender, EventArgs e) {
            if (!state.ControlEnabled || state.CurrentScreen == null || state.connectionStatus != ConnectionStatus.Connected)
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
            if (!state.ControlEnabled || state.CurrentScreen == null || state.connectionStatus != ConnectionStatus.Connected)
                return;

            //Release modifier keys because the remote control window lost focus
            if (listHeldKeysMod.Count > 0) {
                foreach (KeycodeV3 k in listHeldKeysMod)
                    rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                listHeldKeysMod.Clear();

                KeyWinSet(false);

                DebugKeyboard();
            }
        }

        private void Window_Drop(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                if (progressDialog != null && progressDialog.IsBusy)
                    return;

                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                bool doUpload = false;
                bool showExplorer = false;
                using (TaskDialog dialog = new TaskDialog()) {
                    dialog.WindowTitle = "KLC-Finch: Upload File";
                    dialog.MainInstruction = "Upload dropped file to KRCTransferFiles?";
                    dialog.MainIcon = TaskDialogIcon.Information;
                    dialog.CenterParent = true;
                    dialog.Content = files[0];
                    dialog.VerificationText = "Open file explorer when complete";
                    dialog.IsVerificationChecked = true;

                    TaskDialogButton tdbYes = new TaskDialogButton(ButtonType.Yes);
                    TaskDialogButton tdbCancel = new TaskDialogButton(ButtonType.Cancel);
                    dialog.Buttons.Add(tdbYes);
                    dialog.Buttons.Add(tdbCancel);

                    TaskDialogButton button = dialog.ShowDialog(this);
                    doUpload = (button == tdbYes);
                    showExplorer = dialog.IsVerificationChecked;
                }

                if (doUpload) {
                    progressValue = 0;
                    progress = new Progress<int>(newValue => {
                        progressValue = newValue;
                    });
                    progressDialog = new ProgressDialog {
                        //ProgressBarStyle = ProgressBarStyle.MarqueeProgressBar,
                        WindowTitle = "KLC-Finch: Upload File",
                        Text = "Uploading to KRCTransferFiles...",
                        Description = "Source file: " + files[0],
                        ShowCancelButton = false,
                        ShowTimeRemaining = true
                    };
                    progressDialog.DoWork += new DoWorkEventHandler(ProgressDialog_DoWork);
                    progressDialog.Show();

                    rc.UploadDrop(files[0], progress, showExplorer);
                }
            }
        }

        private void Window_KeyUp(object sender, KeyEventArgs e) {
            System.Windows.Forms.KeyEventArgs e2 = e.ToWinforms();
            Window_KeyUp2(e2);
            e.Handled = true;
        }

        private void Window_KeyUp2(System.Windows.Forms.KeyEventArgs e2) {
            if (e2.KeyCode == System.Windows.Forms.Keys.Scroll) {
                KeyHookSet(true);
                return;
            } else if (e2.KeyCode == System.Windows.Forms.Keys.PrintScreen) {
                rc.CaptureNextScreen();
            } else if (!state.ControlEnabled) {
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
                if (KeycodeV3.Unhandled.ContainsKey(e2.KeyCode))
                    return;

                try {
                    KeycodeV3 keykaseya = KeycodeV3.Dictionary[e2.KeyCode];

                    if (endpointOS == Agent.OSProfile.Mac && Settings.MacSwapCtrlWin) {
                        if (KeycodeV3.ModifiersControl.Contains(e2.KeyCode))
                            keykaseya = keywin;
                        else if (e2.KeyCode == System.Windows.Forms.Keys.LWin)
                            keykaseya = keyctrl;
                    }

                    if (keykaseya == null)
                        throw new KeyNotFoundException(e2.KeyCode.ToString());

                    bool removed = (keykaseya.IsMod ? listHeldKeysMod.Remove(keykaseya) : listHeldKeysOther.Remove(keykaseya));

                    rc.SendKeyUp(keykaseya.JavascriptKeyCode, keykaseya.USBKeyCode);

                    if (keyHook.IsActive) {
                        if (e2.KeyCode == System.Windows.Forms.Keys.LWin || e2.KeyCode == System.Windows.Forms.Keys.RWin)
                            KeyWinSet(false);
                    } else {
                        if (keyDownWin && endpointOS != Agent.OSProfile.Mac) {
                            foreach (KeycodeV3 k in listHeldKeysOther)
                                rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                            listHeldKeysOther.Clear();
                            foreach (KeycodeV3 k in listHeldKeysMod)
                                rc.SendKeyUp(k.JavascriptKeyCode, k.USBKeyCode);
                            listHeldKeysMod.Clear();

                            KeyWinSet(false);
                        }
                    }
                } catch {
                    //Console.WriteLine("Up scan: " + e2.KeyCode + " / " + e2.KeyData + " / " + e2.KeyValue);
                }
            }

            DebugKeyboard();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            winScreens.Owner = this;
            KeyHookSet(true);

            rcv.ControlLoaded(rc, state);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e) {
            //Apparently preview is used because arrow keys?

            System.Windows.Forms.KeyEventArgs e2 = e.ToWinforms();
            Window_PreviewKeyDown2(e2);
            e.Handled = true;
        }

        private bool Window_PreviewKeyDown2(System.Windows.Forms.KeyEventArgs e2) {
            if (e2.KeyCode == System.Windows.Forms.Keys.F1) {
                SetControlEnabled(false);
                if(!state.UseMultiScreenPanZoom)
                    rcv.CameraToOverview();
            }

            if (state.connectionStatus != ConnectionStatus.Connected)
                return false;
            if(!state.ControlEnabled) {
                if (state.UseMultiScreenPanZoom) {
                    if (Keyboard.IsKeyDown(Key.W))
                        rcv.MoveUp();
                    else if (Keyboard.IsKeyDown(Key.S))
                        rcv.MoveDown();
                    if (Keyboard.IsKeyDown(Key.A))
                        rcv.MoveLeft();
                    else if (Keyboard.IsKeyDown(Key.D))
                        rcv.MoveRight();
                }
                return false;
            }

            if (e2.KeyCode == System.Windows.Forms.Keys.Pause || e2.KeyCode == System.Windows.Forms.Keys.Scroll) {
                //Done on release
                return true;
            } else if (e2.KeyCode == System.Windows.Forms.Keys.Oemtilde && e2.Control) {
                PerformAutotype(false);
            } else if (e2.KeyCode == System.Windows.Forms.Keys.V && e2.Control && e2.Shift) {
                PerformAutotype(false);
            } else {
                if (KeycodeV3.Unhandled.ContainsKey(e2.KeyCode))
                    return false;

                try {
                    KeycodeV3 keykaseya = KeycodeV3.Dictionary[e2.KeyCode];

                    if (keykaseya == null)
                        throw new KeyNotFoundException(e2.KeyCode.ToString());

                    if (endpointOS == Agent.OSProfile.Mac && Settings.MacSwapCtrlWin) {
                        if (KeycodeV3.ModifiersControl.Contains(e2.KeyCode))
                            keykaseya = keywin;
                        else if (e2.KeyCode == System.Windows.Forms.Keys.LWin)
                            keykaseya = keyctrl;
                    }

                    if (e2.KeyCode == System.Windows.Forms.Keys.LWin || e2.KeyCode == System.Windows.Forms.Keys.RWin)
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
                    //Console.WriteLine("DOWN scan: " + e2.KeyCode + " / " + e2.KeyData + " / " + e2.KeyValue);
                }

                DebugKeyboard();
            }

            return true;
        }

        private void Window_StateChanged(object sender, EventArgs e) {
            if (WindowState == System.Windows.WindowState.Minimized) {
                if (Settings.PowerSaveOnMinimize)
                    rcv.ParentStateChange(false);
            } else {
                rcv.ParentStateChange(true);
            }
        }

        private void ToolScreenFix_Click(object sender, RoutedEventArgs e) {
            if (state.CurrentScreen.rect.Width != state.legacyVirtualWidth) {
                state.CurrentScreen.rect.Width = state.legacyVirtualWidth;
                state.CurrentScreen.rect.Height = state.legacyVirtualHeight;

                if (state.UseMultiScreen) {
                    //Retina hack
                    if (state.UseMultiScreenOverview)
                        rcv.CameraToOverview();
                    else
                        rcv.CameraToCurrentScreen();
                }
            }

            UpdateScreenLayoutReflow();
            state.UseMultiScreenFixAvailable = false;
        }
    }
}