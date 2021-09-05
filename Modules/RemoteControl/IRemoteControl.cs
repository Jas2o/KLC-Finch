using System;
using System.Windows.Input;

namespace KLC_Finch {

    public enum DecodeMode { RawYUV, RawY, BitmapRGB }

    public interface IRemoteControl {

        DecodeMode DecodeMode { get; set; }

        void CaptureNextScreen();
        void SendSecureAttentionSequence();
        void SendKeyUp(int javascriptKeyCode, int uSBKeyCode);
        void SendAutotype(string text);
        void SendPasteClipboard(string text);
        void SendClipboard(string clipboard);
        void Disconnect(string sessionId);
        void SendPanicKeyRelease();
        void Reconnect();
        void UpdateScreens(string jsonstr);
        void ChangeScreen(string screen_id);
        void ChangeTSSession(string session_id);
        void SendKeyDown(int javascriptKeyCode, int uSBKeyCode);
        void UploadDrop(string file, Progress<int> progress);
        void SendMouseUp(MouseButton changedButton);
        void SendMouseWheel(int delta);
        void SendMousePosition(int x, int y);
        void SendMouseDown(MouseButton changedButton);
        void ShowCursor(bool enabled);
        void SendBlackScreenBlockInput(bool blackOutScreen, bool blockMouseKB);
    }
}