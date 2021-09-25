using nucs.JsonSettings;

namespace KLC_Finch {

    public class Settings : JsonSettings {
        public override string FileName { get; set; } = "KLC-Finch-config.json"; //for loading and saving.

        public Settings() {
        }

        public Settings(string fileName) : base(fileName) {
        }

        //--

        public bool AltModulesStartAuto { get; set; } = true;

        public bool AutotypeSkipLengthCheck { get; set; } = false;
        public bool StartControlEnabled { get; set; } = true;
        //[JsonIgnore] private bool ClipboardSyncEnabled { get; set; } = false; //No longer used
        public int ClipboardSync { get; set; } = 2; //Server/Admin only
        public bool DisplayOverlayMouse { get; set; } = false;
        public bool DisplayOverlayKeyboardMod { get; set; } = false;
        public bool DisplayOverlayKeyboardOther { get; set; } = false;
        public bool DisplayOverlayKeyboardHook { get; set; } = false;
        public bool DisplayOverlayPanZoom { get; set; } = false;
        public uint RemoteControlWidth { get; set; } = 1370;  //The same as Kaseya
        public uint RemoteControlHeight { get; set; } = 800;
        public bool KeyboardHook { get; set; } = false;
        public bool MacSwapCtrlWin { get; set; } = false;
        public bool StartMultiScreen { get; set; } = true;
        public bool MultiAltFit { get; set; } = false;
        public bool MultiShowCursor { get; set; } = false;
        public bool ScreenSelectNew { get; set; } = true;
        
        //Graphics
        public int Renderer { get; set; } = 0; //GLControl, GLWpfControl, Canvas
        public bool RendererAlt { get; set; } = false;
        public bool PowerSaveOnMinimize { get; set; } = false;
    }
}