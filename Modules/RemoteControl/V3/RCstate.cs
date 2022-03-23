using NTR;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows;

namespace KLC_Finch {

    public enum ConnectionStatus {
        FirstConnectionAttempt,
        Connected,
        Disconnected
    }

    public enum ScreenStatus {
        Preparing,
        LayoutReady,
        Stable
    }

    public class RCstate : INotifyPropertyChanged {
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

        public RCstate(WindowViewerV3 window) {
            Window = window;
            ListScreen = new List<RCScreen>();

            DependencyObject dep = new DependencyObject();
            if (DesignerProperties.GetIsInDesignMode(dep)) {
                useMultiScreen = true;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool controlEnabled;
        private bool useMultiScreen;
        private bool useMultiScreenOverview;
        private bool useMultiScreenPanZoom;
        private bool useMultiScreenFixAvailable;
        private bool ssClipboardSync;
        public bool ControlEnabled {
            get { return controlEnabled; }
            set {
                controlEnabled = value;
                NotifyPropertyChanged("ControlEnabled");
                NotifyPropertyChanged("ControlEnabledText");
                NotifyPropertyChanged("ControlEnabledTextWeight");
            }
        }
        public string ControlEnabledText {
            get {
                if (controlEnabled)
                    return "Control Enabled";
                else
                    return "Control Disabled";
            }
        }
        public FontWeight ControlEnabledTextWeight {
            get {
                if (controlEnabled)
                    return FontWeights.Normal;
                else
                    return FontWeights.Bold;
            }
        }
        public bool UseMultiScreen {
            get { return useMultiScreen; }
            set {
                useMultiScreen = value;
                NotifyPropertyChanged("UseMultiScreen");
                NotifyPropertyChanged("ScreenModeText");
            }
        }
        public string ScreenModeText {
            get {
                if (useMultiScreen)
                    return "Multi";
                else
                    return "Legacy";
            }
        }
        public bool UseMultiScreenOverview {
            get { return useMultiScreenOverview; }
            set {
                useMultiScreenOverview = value;
                UseMultiScreenFixAvailable = false;
                NotifyPropertyChanged("UseMultiScreenOverview");
                NotifyPropertyChanged("UseMultiScreenOverviewTextWeight");
            }
        }
        public FontWeight UseMultiScreenOverviewTextWeight {
            get {
                if (useMultiScreenOverview)
                    return FontWeights.Bold;
                else
                    return FontWeights.Normal;
            }
        }
        public bool UseMultiScreenPanZoom {
            get { return useMultiScreenPanZoom; }
            set {
                useMultiScreenPanZoom = value;
                NotifyPropertyChanged("UseMultiScreenPanZoom");
            }
        }
        public bool UseMultiScreenFixAvailable {
            get { return useMultiScreenFixAvailable; }
            set {
                useMultiScreenFixAvailable = useMultiScreen ? value : false;
                NotifyPropertyChanged("UseMultiScreenFixAvailable");
            }
        }
        public bool SsClipboardSync {
            get { return ssClipboardSync; }
            set {
                ssClipboardSync = value;
                NotifyPropertyChanged("SsClipboardSync");
                NotifyPropertyChanged("SsClipboardSyncReceiveOnly");
            }
        }
        public bool SsClipboardSyncReceiveOnly {
            get { return !ssClipboardSync; }
        }

        public RCScreen GetScreenUsingMouse(int x, int y) {
            if (CurrentScreen == null)
                return null;
            if (CurrentScreen.rect.Contains(x, y))
                return CurrentScreen;

            //This doesn't yet work in Canvas
            foreach (RCScreen screen in ListScreen) {
                if (screen == CurrentScreen)
                    continue;

                if (screen.rect.Contains(x, y))
                    return screen;
            }
            return null;
        }

        public void SetVirtual(int virtualX, int virtualY, int virtualWidth, int virtualHeight) {
            if (UseMultiScreen) {
                virtualViewWant = new Rectangle(virtualX, virtualY, virtualWidth, virtualHeight);
            } else {
                this.legacyVirtualWidth = virtualWidth;
                this.legacyVirtualHeight = virtualHeight;
                virtualCanvas = virtualViewWant = new Rectangle(0, 0, virtualWidth, virtualHeight);
            }

            virtualRequireViewportUpdate = true;
        }

        public bool WindowIsActive() {
            return Window.IsActive;
        }

        public void ZoomIn() {
            if (!UseMultiScreen)
                return;
            if (virtualViewWant.Width - 200 < 0 || virtualViewWant.Height - 200 < 0)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X + 100, virtualViewWant.Y + 100, virtualViewWant.Width - 200, virtualViewWant.Height - 200);
            virtualRequireViewportUpdate = true;
        }

        public void ZoomOut() {
            if (!UseMultiScreen)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X - 100, virtualViewWant.Y - 100, virtualViewWant.Width + 200, virtualViewWant.Height + 200);
            virtualRequireViewportUpdate = true;
        }

        private void NotifyPropertyChanged([CallerMemberName] string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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