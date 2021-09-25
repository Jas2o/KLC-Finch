using NTR;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

    public class RCstate {

        public bool autotypeAlwaysConfirmed = false;
        public string BaseTitle;
        public ConnectionStatus connectionStatus = ConnectionStatus.FirstConnectionAttempt;
        public bool controlEnabled = false;
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
        public bool useMultiScreen;
        public bool useMultiScreenOverview;
        public bool useMultiScreenPanZoom;
        public Rectangle virtualCanvas, virtualViewWant, virtualViewNeed;
        public bool virtualRequireViewportUpdate = false;
        public int virtualWidth, virtualHeight;
        public WindowViewerV3 Window;
        public bool windowActivatedMouseMove;

        public RCstate(WindowViewerV3 window) {
            Window = window;
            ListScreen = new List<RCScreen>();
        }

        public RCScreen GetScreenUsingMouse(int x, int y) {
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
            if (useMultiScreen) {
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
            if (!useMultiScreen)
                return;
            if (virtualViewWant.Width - 200 < 0 || virtualViewWant.Height - 200 < 0)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X + 100, virtualViewWant.Y + 100, virtualViewWant.Width - 200, virtualViewWant.Height - 200);
            virtualRequireViewportUpdate = true;
        }

        public void ZoomOut() {
            if (!useMultiScreen)
                return;

            virtualViewWant = new Rectangle(virtualViewWant.X - 100, virtualViewWant.Y - 100, virtualViewWant.Width + 200, virtualViewWant.Height + 200);
            virtualRequireViewportUpdate = true;
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
