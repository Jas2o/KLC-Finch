using NTR;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows;

namespace KLC_Finch {

    public abstract class WindowViewer : Window {

        protected IRemoteControl rc;
        protected RCstate state;

        public WindowViewer() : base() {
            state = new RCstate(null);
        }

        public List<RCScreen> GetListScreen() {
            return state.ListScreen;
        }

            public RCScreen GetCurrentScreen() {
            return state.CurrentScreen;
        }

        public abstract void AddTSSession(string session_id, string session_name);

        public abstract void ClearApproval();

        public abstract void ClearTSSessions();

        public abstract void LoadCursor(int cursorX, int cursorY, int cursorWidth, int cursorHeight, int cursorHotspotX, int cursorHotspotY, byte[] remaining);

        public abstract void LoadTexture(int width, int height, Bitmap decomp);

        public abstract void LoadTextureRaw(byte[] buffer, int width, int height, int stride);

        public abstract void NotifySocketClosed(string sessionId);

        public abstract void ReceiveClipboard(string content);

        public abstract void SetControlEnabled(bool value, bool isStart = false);

        public abstract void SetTitle(string title, bool modePrivate);

        public abstract void SetApprovalAndSpecialNote(LibKaseya.Enums.NotifyApproval rcNotify, int machineShowToolTip, string machineNote, string machineNoteLink);

        public abstract void SetSessionID(string sessionId);

        public abstract void UpdateLatency(long ms);

        public abstract void UpdateScreenLayout(dynamic json, string jsonstr = "");

        public bool GetStatePowerSaving() {
            return state.powerSaving;
        }
    }
}