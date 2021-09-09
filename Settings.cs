using nucs.JsonSettings;

namespace KLC_Finch {

    public class Settings : JsonSettings {
        public override string FileName { get; set; } = "KLC-Finch-config.json"; //for loading and saving.

        public Settings() {
        }

        public Settings(string fileName) : base(fileName) {
        }

        //--

        public bool AutotypeSkipLengthCheck { get; set; } = false;
        public bool StartControlEnabled { get; set; } = true;
        //[JsonIgnore] private bool ClipboardSyncEnabled { get; set; } = false; //No longer used
        public int ClipboardSync { get; set; } = 2; //Server/Admin only
        public bool DisplayOverlayMouse { get; set; } = false;
        public bool DisplayOverlayKeyboardMod { get; set; } = false;
        public bool DisplayOverlayKeyboardOther { get; set; } = false;
        public bool DisplayOverlayKeyboardHook { get; set; } = false;
        public uint RemoteControlWidth { get; set; } = 1370;  //The same as Kaseya
        public uint RemoteControlHeight { get; set; } = 800;
        public bool KeyboardHook { get; set; } = false;
        public bool MacSwapCtrlWin { get; set; } = false;
        public bool StartMultiScreen { get; set; } = true;
        public bool MultiAltFit { get; set; } = false;
        public bool MultiShowCursor { get; set; } = false;
        public bool ScreenSelectNew { get; set; } = true;
        
        //public int GraphicsMode { get; set; } = 0; //OpenGL YUV, OpenGL RGB, Canvas RGB, Canvas Y
        public GraphicsMode GraphicsModeV3 { get; set; } = GraphicsMode.OpenGL_YUV;

        //[JsonIgnore] private bool UseYUVShader { get; set; } = true;
        //[JsonIgnore] private bool ForceCanvas { get; set; } = false;
        public bool PowerSaveOnMinimize { get; set; } = false;
    }
}