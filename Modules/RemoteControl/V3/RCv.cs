using NTR;
using System.Windows.Controls;

namespace KLC_Finch {

    public abstract class RCv : UserControl {
        protected IRemoteControl rc;
        protected RCstate state;

        public RCv(IRemoteControl rc, RCstate state) : base() {
            this.rc = rc;
            this.state = state;
        }

        public abstract bool SupportsLegacy { get; }

        //public abstract bool SupportsBaseZoom { get; }

        public abstract void CameraFromClickedScreen(RCScreen screen, bool moveCamera = true);

        public abstract void CameraToCurrentScreen();

        public abstract void CameraToOverview();

        public abstract void CheckHealth();

        public abstract void ControlLoaded(IRemoteControl rc, RCstate state);

        public abstract void ControlUnload();

        public abstract void DisplayApproval(bool visible);

        public abstract void DisplayControl(bool enabled);

        public abstract void DisplayDebugKeyboard(string strKeyboard);

        public abstract void DisplayDebugMouseEvent(int X, int Y);

        public abstract void DisplayKeyHook(bool enabled);

        public abstract void ParentStateChange(bool visible);

        public abstract void Refresh();

        public abstract void SetCanvas(int virtualX, int virtualY, int virtualWidth, int virtualHeight);

        public abstract bool SwitchToLegacy();

        public abstract bool SwitchToMultiScreen();

        public abstract void UpdateScreenLayout(int lowestX, int lowestY, int highestX, int highestY);

        public abstract void TogglePanZoom();
        public abstract void MoveUp();
        public abstract void MoveDown();
        public abstract void MoveLeft();
        public abstract void MoveRight();
    }
}