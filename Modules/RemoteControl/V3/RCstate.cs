using LibKaseya;
using NTR;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using static LibKaseya.Enums;

namespace KLC_Finch
{

    public enum ConnectionStatus
    {
        FirstConnectionAttempt,
        Connected,
        Disconnected
    }

    public enum ScreenStatus
    {
        Preparing,
        LayoutReady,
        Stable
    }

    public class RCstate : INotifyPropertyChanged
    {
        private static string[] arrAdmins = new string[] { "administrator", "brandadmin", "adminc", "company" };

        public Agent.OSProfile endpointOS;
        public string endpointLastUser;

        public bool autotypeAlwaysConfirmed = false;
        public string BaseTitle;
        public ConnectionStatus connectionStatus = ConnectionStatus.FirstConnectionAttempt;
        public RCScreen CurrentScreen;
        public long lastLatency;
        public RCScreen legacyScreen;
        public int legacyVirtualWidth, legacyVirtualHeight;
        public List<RCScreen> ListScreen;
        public bool mouseHeldLeft = false;
        public bool mouseHeldRight = false;
        public bool powerSaving;
        public RCScreen previousScreen;
        public bool requiredApproval = false;
        public ScreenStatus screenStatus = ScreenStatus.Preparing;
        public string sessionId;
        public bool socketAlive = false;
        public TextureCursor textureCursor = null;
        public TextureScreen textureLegacy;
        public Rectangle virtualCanvas, virtualViewWant, virtualViewNeed;
        public bool virtualRequireViewportUpdate = false;
        public int virtualWidth, virtualHeight;
        public WindowViewerV3 Window;
        public bool windowActivatedMouseMove;
        public readonly List<TSSession> listTSSession = new List<TSSession>();
        public TSSession currentTSSession = null;

        public RCstate()
        {
            ListScreen = new List<RCScreen>();

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                useMultiScreen = true;
            }

            legacyScreen = new RCScreen("Legacy", "Legacy", 800, 600, 0, 0);
        }

        public RCstate(WindowViewerV3 window)
        {
            Window = window;
            ListScreen = new List<RCScreen>();

            if (DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            {
                useMultiScreen = true;
            }
        }

        public void SetSessionID(string sessionId)
        {
            this.sessionId = sessionId;

            if (sessionId != null)
            {
                socketAlive = true;
                //connectionStatus = ConnectionStatus.Connected;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool controlEnabled;
        private bool useMultiScreen;
        private bool useMultiScreenOverview;
        private bool useMultiScreenPanZoom;
        private bool useMultiScreenFixAvailable;
        private bool ssClipboardSync;
        private bool hasFileTransferWaiting;

        public void SetTitle(string title, RC mode)
        {
            switch (mode)
            {
                case RC.Shared:
                    BaseTitle = title + "::Shared";
                    break;
                case RC.Private:
                    BaseTitle = title + "::Private";
                    //toolReconnect.Header = "Reconnect (lose private session)";
                    break;
                case RC.OneClick:
                    BaseTitle = title + "::1-Click";
                    //toolReconnect.Header = "Reconnect (lose 1-Click session)";
                    break;
                default:
                    BaseTitle = title + "::UNSUPPORTED";
                    break;
            }
        }

        public void ForceRefresh()
        {
            //This is intended to be used when switching active RC/State.
            NotifyPropertyChanged("ControlEnabled");
            NotifyPropertyChanged("ControlEnabledText");
            NotifyPropertyChanged("ControlEnabledTextWeight");

            NotifyPropertyChanged("UseMultiScreen");
            NotifyPropertyChanged("ScreenModeText");

            NotifyPropertyChanged("UseMultiScreenOverview");
            NotifyPropertyChanged("UseMultiScreenOverviewTextWeight");

            NotifyPropertyChanged("UseMultiScreenPanZoom");

            NotifyPropertyChanged("UseMultiScreenFixAvailable");

            NotifyPropertyChanged("SsClipboardSync");
            NotifyPropertyChanged("SsClipboardSyncReceiveOnly");

            NotifyPropertyChanged("HasFileTransferWaiting");
        }

        public bool ControlEnabled
        {
            get { return controlEnabled; }
            set
            {
                controlEnabled = value;
                NotifyPropertyChanged("ControlEnabled");
                NotifyPropertyChanged("ControlEnabledText");
                NotifyPropertyChanged("ControlEnabledTextWeight");
            }
        }
        public string ControlEnabledText
        {
            get
            {
                if (controlEnabled)
                    return "Control Enabled";
                else
                    return "Control Disabled";
            }
        }
        public FontWeight ControlEnabledTextWeight
        {
            get
            {
                if (controlEnabled)
                    return FontWeights.Normal;
                else
                    return FontWeights.Bold;
            }
        }
        public bool UseMultiScreen
        {
            get { return useMultiScreen; }
            set
            {
                useMultiScreen = value;
                NotifyPropertyChanged("UseMultiScreen");
                NotifyPropertyChanged("ScreenModeText");
            }
        }
        public string ScreenModeText
        {
            get
            {
                if (useMultiScreen)
                    return "Multi";
                else
                    return "Legacy";
            }
        }
        public bool UseMultiScreenOverview
        {
            get { return useMultiScreenOverview; }
            set
            {
                useMultiScreenOverview = value;
                UseMultiScreenFixAvailable = false;
                NotifyPropertyChanged("UseMultiScreenOverview");
                NotifyPropertyChanged("UseMultiScreenOverviewTextWeight");
            }
        }
        public FontWeight UseMultiScreenOverviewTextWeight
        {
            get
            {
                if (useMultiScreenOverview)
                    return FontWeights.Bold;
                else
                    return FontWeights.Normal;
            }
        }
        public bool UseMultiScreenPanZoom
        {
            get { return useMultiScreenPanZoom; }
            set
            {
                useMultiScreenPanZoom = value;
                NotifyPropertyChanged("UseMultiScreenPanZoom");
            }
        }
        public bool UseMultiScreenFixAvailable
        {
            get { return useMultiScreenFixAvailable; }
            set
            {
                useMultiScreenFixAvailable = useMultiScreen ? value : false;
                NotifyPropertyChanged("UseMultiScreenFixAvailable");
            }
        }
        public bool SsClipboardSync
        {
            get { return ssClipboardSync; }
            set
            {
                ssClipboardSync = value;
                NotifyPropertyChanged("SsClipboardSync");
                NotifyPropertyChanged("SsClipboardSyncReceiveOnly");
            }
        }
        public bool SsClipboardSyncReceiveOnly
        {
            get { return !ssClipboardSync; }
        }

        public bool HasFileTransferWaiting
        {
            get { return hasFileTransferWaiting; }
            set
            {
                hasFileTransferWaiting = value;
                NotifyPropertyChanged("HasFileTransferWaiting");
            }
        }

		/*
        public void UpdateScreenLayout(dynamic json)
        {
            screenStatus = ScreenStatus.Preparing;

            ListScreen.Clear();
            previousScreen = CurrentScreen = null;

            string default_screen = json["default_screen"].ToString();
            connectionStatus = ConnectionStatus.Connected;

            foreach (dynamic screen in json["screens"])
            {
                string screen_id = screen["screen_id"].ToString(); //int or BigInteger
                string screen_name = (string)screen["screen_name"];
                int screen_height = (int)screen["screen_height"];
                int screen_width = (int)screen["screen_width"];
                int screen_x = (int)screen["screen_x"];
                int screen_y = (int)screen["screen_y"];

                //Add Screen
                RCScreen newScreen = new RCScreen(screen_id, screen_name, screen_height, screen_width, screen_x, screen_y);
                ListScreen.Add(newScreen);

                if (screen_id == default_screen)
                {
                    CurrentScreen = newScreen;
                    //Legacy
                    legacyVirtualHeight = CurrentScreen.rect.Height;
                    legacyVirtualWidth = CurrentScreen.rect.Width;
                    virtualRequireViewportUpdate = true;
                }
            }

            int lowestX = ListScreen.Min(x => x.rect.X);
            int lowestY = ListScreen.Min(x => x.rect.Y);
            int highestX = ListScreen.Max(x => x.rect.Right);
            int highestY = ListScreen.Max(x => x.rect.Bottom);
            virtualCanvas = new Rectangle(lowestX, lowestY, highestX - lowestX, highestY - lowestY);
            screenStatus = ScreenStatus.LayoutReady;
        }
        */

        public RCScreen GetScreenUsingMouse(int x, int y)
        {
            if (CurrentScreen == null)
                return null;
            if (CurrentScreen.rect.Contains(x, y))
                return CurrentScreen;

            //This doesn't yet work in Canvas
            foreach (RCScreen screen in ListScreen)
            {
                if (screen == CurrentScreen)
                    continue;

                if (screen.rect.Contains(x, y))
                    return screen;
            }
            return null;
        }

        public RCScreen GetClosestScreenUsingMouse(int x, int y)
        {
            if (CurrentScreen == null)
                return null;
            if (CurrentScreen.rectEdge.Contains(x, y))
                return CurrentScreen;

            //This doesn't yet work in Canvas
            foreach (RCScreen screen in ListScreen)
            {
                if (screen == CurrentScreen)
                    continue;

                if (screen.rectEdge.Contains(x, y))
                    return screen;
            }
            return null;
        }

        public void SetVirtual(int virtualX, int virtualY, int virtualWidth, int virtualHeight)
        {
            if (UseMultiScreen)
            {
                virtualViewWant = new Rectangle(virtualX, virtualY, virtualWidth, virtualHeight);
            }
            else
            {
                this.legacyVirtualWidth = virtualWidth;
                this.legacyVirtualHeight = virtualHeight;
                virtualCanvas = virtualViewWant = new Rectangle(0, 0, virtualWidth, virtualHeight);
            }

            virtualRequireViewportUpdate = true;
        }

        public bool WindowIsActive()
        {
            return Window.IsActive;
        }

        public void ZoomIn()
        {
            if (!UseMultiScreen)
                return;
            if (virtualViewWant.Width - 200 < 0 || virtualViewWant.Height - 200 < 0)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X + 100, virtualViewWant.Y + 100, virtualViewWant.Width - 200, virtualViewWant.Height - 200);
            virtualRequireViewportUpdate = true;
        }

        public void ZoomOut()
        {
            if (!UseMultiScreen)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X - 100, virtualViewWant.Y - 100, virtualViewWant.Width + 200, virtualViewWant.Height + 200);
            virtualRequireViewportUpdate = true;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void LoadCursor(int cursorX, int cursorY, int cursorWidth, int cursorHeight, int cursorHotspotX, int cursorHotspotY, byte[] remaining)
        {
            if (textureCursor != null)
                textureCursor.Load(new Rectangle(cursorX - cursorHotspotX, cursorY - cursorHotspotY, cursorWidth, cursorHeight), remaining);
        }
    }

    /*
    public enum GraphicsMode {
        OpenGL_YUV = 0,
        OpenGL_RGB = 1,
        OpenGL_WPF_YUV = 10,
        OpenGL_WPF_RGB = 11,
        Canvas_RGB = 20,
        Canvas_Y = 21
    }
    */
}