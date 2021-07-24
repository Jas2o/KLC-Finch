using Newtonsoft.Json;
using nucs.JsonSettings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLC_Finch {
    public class Settings : JsonSettings {
        public override string FileName { get; set; } = "KLC-Finch-config.json"; //for loading and saving.
        public Settings() { }
        public Settings(string fileName) : base(fileName) { }

        //--

        public bool AutotypeSkipLengthCheck { get; set; } = false;
        public bool StartControlEnabled { get; set; } = true;
        public bool ClipboardSyncEnabled { get; set; } = false;
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
        public bool UseYUVShader { get; set; } = true;
        public bool ForceCanvas { get; set; } = false;
    }
}
