using nucs.JsonSettings;

namespace KLC_Finch {

    public class Settings : JsonSettings {
        public override string FileName { get; set; } = "KLC-Finch-config.json"; //for loading and saving.

        public Settings() {
        }

        public Settings(string fileName) : base(fileName) {
        }

        //Alternative
        public bool AltModulesStartAuto { get; set; } = true;
        public bool AltModulesStartAutoMacStaticImage { get; set; } = true;
        public bool AltModulesDashboardRefresh { get; set; } = true;
        public bool AltShowWarnings { get; set; } = false;
        public bool AltShowAlphaTab { get; set; } = false;

        //RC: Debug Text
        public bool DisplayOverlayMouse { get; set; } = false;
        public bool DisplayOverlayKeyboardMod { get; set; } = false;
        public bool DisplayOverlayKeyboardOther { get; set; } = false;
        public bool DisplayOverlayKeyboardHook { get; set; } = false;
        public bool DisplayOverlayPanZoom { get; set; } = false;

        //RC: Control
        public bool AutotypeSkipLengthCheck { get; set; } = false;
        public bool StartControlEnabled { get; set; } = false;
        //[JsonIgnore] private bool ClipboardSyncEnabled { get; set; } = false; //No longer used
        public int ClipboardSync { get; set; } = 2; //Server/Admin only
        public bool KeyboardHook { get; set; } = false;
        public bool MacSwapCtrlWin { get; set; } = true;
        public bool MacSafeKeys { get; set; } = true;

        //RC: Multi-Screen
        public bool StartMultiScreen { get; set; } = true;
        public bool StartMultiScreenExceptMac { get; set; } = false;
        public bool MultiAltFit { get; set; } = true;
        public bool MultiShowCursor { get; set; } = true;
        public bool ScreenSelectNew { get; set; } = true;

        //RC: Graphics
        public int Renderer { get; set; } = 0; //GLControl, GLWpfControl, Canvas
        public bool RendererAlt { get; set; } = false;
        public bool PowerSaveOnMinimize { get; set; } = true;

        //RC: Initial Window Size
        public uint RemoteControlWidth { get; set; } = 1370;  //The same as Kaseya
        public uint RemoteControlHeight { get; set; } = 800;
        public int Downscale { get; set; } = 0;
    }
}