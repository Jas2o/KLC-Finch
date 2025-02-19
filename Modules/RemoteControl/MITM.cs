using Fleck;
using LibKaseya;
using System;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace KLC_Finch {
    class MITM {

        private static Thread threadSendText;

        private static byte[] GetJsonMessage(Enums.KaseyaMessageTypes messageType, string json) {
            int jsonLen = json.Length;
            byte[] jsonBuffer = Encoding.UTF8.GetBytes(json);

            byte[] tosend = new byte[jsonLen + 5];
            tosend[0] = (byte)messageType;
            byte[] tosendPrefix = BitConverter.GetBytes(jsonLen).Reverse().ToArray();
            Array.Copy(tosendPrefix, 0, tosend, 1, tosendPrefix.Length);
            Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);

            return tosend;
        }

        public static byte[] GetSendKey(KeycodeV2 keycode, bool pressed) {
            string sendjson = "{\"keyboard_layout_handle\":\"0\",\"keyboard_layout_local\":false,\"lock_states\":2,\"pressed\":" + pressed.ToString().ToLower() + ",\"usb_keycode\":" + keycode.USBKeyCode + ",\"virtual_key\":" + keycode.JavascriptKeyCode + "}";
            return GetJsonMessage(Enums.KaseyaMessageTypes.Keyboard, sendjson);
        }

        public static void SendText(IWebSocketConnection socket, string text, int speedPreset = 0) {

            //Evil twins
            text = text.Replace('“', '"').Replace('”', '"').Replace('–', '-');

            threadSendText = new Thread(() => {
                //Fast
                int delayShift = 0;
                int delayKey = 0;

                if (speedPreset == 1) { //Average
                    delayShift = 25;
                    delayKey = 10;
                } else if (speedPreset == 2) { //Slow
                    delayShift = 75;
                    delayKey = 50;
                }

                //On a Windows computer the delays can be pretty short, however on a Mac even these long 75/50 delays tend to break.

                KeycodeV2 keyShift = KeycodeV2.List.Find(x => x.Key == Keys.ShiftKey);

                string lower = "`1234567890-=qwertyuiop[]\\asdfghjkl;'zxcvbnm,./";
                string upper = "~!@#$%^&*()_+QWERTYUIOP{}|ASDFGHJKL:\"ZXCVBNM<>?";
                bool shift = false;

                foreach (char c in text) {
                    if (upper.Contains(c) && !shift) {
                        shift = true;
                        socket.Send(GetSendKey(keyShift, true));
                        if (delayShift != 0)
                            Thread.Sleep(delayShift);
                    } else if (lower.Contains(c) && shift) {
                        shift = false;
                        socket.Send(GetSendKey(keyShift, false));
                        if (delayShift != 0)
                            Thread.Sleep(delayShift);
                    }

                    KeycodeV2 code = KeycodeV2.List.Find(x => x.Key == (Keys)(KeycodeV2.VkKeyScan(c) & 0xff));
                    if (code != null) {
                        socket.Send(MITM.GetSendKey(code, true));
                        if (delayKey != 0)
                            Thread.Sleep(delayKey);
                        socket.Send(MITM.GetSendKey(code, false));
                        if (delayKey != 0)
                            Thread.Sleep(delayKey);
                    }
                }

                if (shift) {
                    shift = false;
                    socket.Send(MITM.GetSendKey(keyShift, false));
                }
            });
            threadSendText.Start();
        }

    }
}
