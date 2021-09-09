using NTR;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLC_Finch {
    public class RCstate {

        public WindowViewerV3 Window;

        public string sessionId;
        public string BaseTitle;
        public bool socketAlive = false;
        public ConnectionStatus connectionStatus = ConnectionStatus.FirstConnectionAttempt;
        public long lastLatency;
        public bool powerSaving;
        public bool requiredApproval = false;
        public bool autotypeAlwaysConfirmed = false;
        public ScreenStatus screenStatus = ScreenStatus.Preparing;
        public int virtualWidth, virtualHeight;
        public int legacyVirtualWidth, legacyVirtualHeight;
        public List<RCScreen> ListScreen;
        public RCScreen CurrentScreen;
        public RCScreen previousScreen;
        public TextureCursor textureCursor = null;
        public TextureScreen textureLegacy;
        public bool windowActivatedMouseMove;
        public bool controlEnabled = false;
        public bool useMultiScreen;
        public bool useMultiScreenOverview;
        public Rectangle virtualCanvas, virtualViewWant, virtualViewNeed;
        public bool virtualRequireViewportUpdate = false;

        public bool mouseHeldLeft = false;
        public bool mouseHeldRight = false;

        public RCstate(WindowViewerV3 window) {
            Window = window;
            ListScreen = new List<RCScreen>();
        }

        public bool WindowIsActive() {
            return Window.IsActive;
        }

        public RCScreen GetScreenUsingMouse(int x, int y) {
            //This doesn't yet work in Canvas
            foreach (RCScreen screen in ListScreen) {
                if (screen.rect.Contains(x, y)) {
                    return screen;
                }
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

    }

    public enum ScreenStatus {
        Preparing,
        LayoutReady,
        Stable
    }

    public enum ConnectionStatus {
        FirstConnectionAttempt,
        Connected,
        Disconnected
    }

    public enum GraphicsMode {
        OpenGL_YUV = 0,
        OpenGL_RGB = 1,
        OpenGL_WPF_YUV = 10,
        OpenGL_WPF_RGB = 11,
        Canvas_RGB = 20,
        Canvas_Y = 21
    }
}
