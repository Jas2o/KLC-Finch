using LibKaseya;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WatsonWebsocket;
using static LibKaseya.Enums;

namespace KLC {
    public class WsY2 {

        private LiveConnectSession Session;
        public int PortY { get; private set; }
        public string Module { get; private set; }

        private WatsonWsClient WebsocketY;
        public string Client;
        public int ClientPort;

        private string PathAndQuery;

        //Need a better place for this as it's only used by the Remote Control module
        private List<KeycodeV2> listHeldMods; //Modifier keys, they can stay down between any keys
        private List<KeycodeV2> listHeldKeys; //Non-monifier keys, these should auto release any other non-modifier keys

        public WsY2(LiveConnectSession session, int portY, string PathAndQuery) {
            //Type 2 - is started
            Session = session;
            PortY = portY;
            this.PathAndQuery = PathAndQuery;
            Module = PathAndQuery.Split('/')[2];

            Console.WriteLine("New Y2 " + PathAndQuery);

            if (Module == "remotecontrol") {
                listHeldKeys = new List<KeycodeV2>();
                listHeldMods = new List<KeycodeV2>();
            }

            if (PortY == 0)
                throw new Exception();

            WebsocketY = new WatsonWsClient(new Uri("ws://127.0.0.1:" + PortY + PathAndQuery + "?Y2"));

            WebsocketY.ServerConnected += WebsocketY2_ServerConnected;
            WebsocketY.ServerDisconnected += WebsocketY2_ServerDisconnected;
            WebsocketY.MessageReceived += WebsocketY2_MessageReceived;

            WebsocketY.Start();
        }

        private void WebsocketY2_MessageReceived(object sender, MessageReceivedEventArgs e) {
            //Session.Parent.LogText("Y2 Message");
            if (Client == null || Client == "") {
                Console.WriteLine("Y1 Needs to know B's client!");
                //Session.Parent.Log(Side.MITM, PortY, PortY, "Y2 needs to know B's client!");
                while (Client == null || Client == "") {
                    Task.Delay(10);
                }
            }

            bool doNothing = false;

            /*
            if (Module == "remotecontrol") {
                #region MITM
                if (e.Data.Length > 6 && e.Data[5] == '{') {
                    //Maybe some MITM?
                    KaseyaMessageTypes kmtype = (KaseyaMessageTypes)e.Data[0];
                    byte[] bLen = new byte[4];
                    Array.Copy(e.Data, 1, bLen, 0, 4);
                    Array.Reverse(bLen); //Endianness
                    int jLen = BitConverter.ToInt32(bLen, 0);
                    string message = Encoding.ASCII.GetString(e.Data, 5, jLen);
                    dynamic json = JsonConvert.DeserializeObject(message);

                    int remStart = 5 + jLen;
                    int remLength = e.Data.Length - remStart;
                    byte[] remaining = new byte[remLength];
                    if (remLength > 0)
                        Array.Copy(e.Data, remStart, remaining, 0, remLength);

                    switch (kmtype) {
                        case KaseyaMessageTypes.FrameAcknowledgement:
                            //if (Session.Parent.EnableFrameAckHack)
                                //doNothing = true;
                            break;

                        case KaseyaMessageTypes.Mouse:
                            #region Mouse - Middle click to auto type up to 50 characters from the clipboard
                            KaseyaMouseEventTypes kmet = (KaseyaMouseEventTypes)json["type"];
                            if (kmet == KaseyaMouseEventTypes.Button && (int)json["button"] == 2) { //Middle mouse button
                                if (Session.Parent.EnableAutoType) {
                                    doNothing = true;
                                    if ((bool)json["button_down"]) {

                                        //Test
                                        //Session.Parent.ActivateOverlay(WebsocketB, Client);

                                        string text = "";

                                        //This may cause AV to think we're malware
                                        Thread tc = new Thread(() => text = Clipboard.GetText().Trim());
                                        tc.SetApartmentState(ApartmentState.STA);
                                        tc.Start();
                                        Thread.Sleep(1);
                                        tc.Join();
                                        Thread.Sleep(1);

                                        if (text.Length < 51 && !text.Contains('\n') && !text.Contains('\r')) {
                                            Session.Parent.LogText("Attempt autotype of " + text, "autotype");
                                            MITM.SendText(WebsocketB, Client, text, Session.Parent.AutoTypeSpeed);
                                            //Session.Parent.Log(Side.MITM, PortY, PortY, "Send keys: " + text);
                                        } else {
                                            Session.Parent.LogText("Autotype blocked: too long or had a new line character");
                                        }
                                    }
                                }
                            }
                            #endregion
                            break;

                        case KaseyaMessageTypes.Keyboard:
                            #region Keyboard key press release order fix (but not Macs)
                            //The goal of this MITM fix is to prevent keys from being sent in the wrong order by releasing them earlier than as said by Live Connect!

                            if (Session.Parent.EnableKeyboardReleaseHack) {

                                //Using the USB keycode is unreliable
                                KeycodeV2 keykaseya = KeycodeV2.List.Find(x => x.JavascriptKeyCode == (int)json["virtual_key"]);
                                if (keykaseya == null) {
                                    doNothing = true;

                                    KeycodeV2 keykaseyaUN = KeycodeV2.ListUnhandled.Find(x => x.JavascriptKeyCode == (int)json["virtual_key"]);
                                    if (keykaseyaUN == null)
                                        Session.Parent.LogText("Unknown key JS: " + json["virtual_key"] + " - USB: " + json["usb_keycode"]);
                                    else if (keykaseyaUN.Key == Keys.PrintScreen) {
                                        if (!(bool)json["pressed"])
                                            WebsocketB.CaptureNextScreen();
                                    } else if (keykaseyaUN.Key == Keys.Pause) {
                                        if (!(bool)json["pressed"]) {
                                            foreach (int jskey in KeycodeV2.ModifiersJS) {
                                                KeycodeV2 key = KeycodeV2.List.Find(x => x.JavascriptKeyCode == jskey);
                                                WebsocketB.Send(Client, MITM.GetSendKey(key, false));
                                            }
                                            Session.Parent.LogText("MITM release all modifiers", "keyrelease");
                                        }
                                    } else {
                                        //Session.Parent.Invoke(new Action(() => Session.Parent.Activate()));
                                        MITM.HandleKey(keykaseyaUN);
                                    }
                                } else {

                                    bool keyIsMod = KeycodeV2.ModifiersJS.Contains(keykaseya.JavascriptKeyCode);

                                    if ((bool)json["pressed"]) {
                                        if (keyIsMod) {
                                            if (!listHeldMods.Contains(keykaseya))
                                                listHeldMods.Add(keykaseya);
                                            else
                                                doNothing = true;
                                        } else {
                                            //doMITM = true;

                                            foreach (KeycodeV2 held in listHeldKeys) {
                                                if (held == keykaseya)
                                                    continue;

                                                string sendjson = "{\"keyboard_layout_handle\":\"0\",\"keyboard_layout_local\":false,\"lock_states\":2,\"pressed\":false,\"usb_keycode\":" + held.USBKeyCode + ",\"virtual_key\":" + held.JavascriptKeyCode + "}";
                                                byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);
                                                int jsonLen = jsonBuffer.Length;

                                                byte[] tosend = new byte[jsonLen + 5];
                                                tosend[0] = (byte)KaseyaMessageTypes.Keyboard;
                                                byte[] tosendPrefix = BitConverter.GetBytes(jsonLen).Reverse().ToArray();
                                                Array.Copy(tosendPrefix, 0, tosend, 1, tosendPrefix.Length);
                                                Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);

                                                Session.Parent.LogText("MITM release key: " + held.Display, "keyrelease");
                                                WebsocketB.Send(Client, tosend);
                                                //Session.Parent.LogOld(Side.MITM, PortY, PortY, tosend);
                                            }
                                            listHeldKeys.Clear();

                                            listHeldKeys.Add(keykaseya);
                                        }
                                    } else {
                                        if (keyIsMod)
                                            listHeldMods.Remove(keykaseya);
                                        else
                                            listHeldKeys.Remove(keykaseya);
                                    }
                                }
                            } //End key release hack
                              //End keyboard message
                            #endregion
                            break;

                        case KaseyaMessageTypes.Clipboard:
                            string clipboard = Encoding.ASCII.GetString(remaining);

                            if (clipboard.Length == 0)
                                doNothing = true;
                            else {
                                Session.Parent.LogText("Clipboard send: [" + clipboard + "]", "clipboard");
                                if (!Session.Parent.EnableClipboardHostToRemote)
                                    doNothing = true;
                            }
                            break;
                    }
                }
                #endregion
            }
            */

            if (doNothing) {
                //Session.Parent.Log(Side.MITM, PortY, WebsocketB.PortB, "???");
            } else {
                throw new NotImplementedException();
                /*
                string messageY = Encoding.UTF8.GetString(e.Data);
                if (messageY[0] == '{')
                    WebsocketB.Send(Client, messageY);
                //Session.ServerB.SendAsync(Client, messageY);
                else
                    WebsocketB.Send(Client, e.Data);
                //Session.ServerB.SendAsync(Client, e.Data);
                */
            }
        }

        private void WebsocketY2_ServerDisconnected(object sender, EventArgs e) {
            Console.WriteLine("Y2 Disconnected " + Module);

            //if (Module != "files")
                //WebsocketB.ServerB.DisconnectClient(Client);
        }

        private void WebsocketY2_ServerConnected(object sender, EventArgs e) {
            Console.WriteLine("Y2 Connect " + Module);
        }

        public void Send(byte[] data) {
            try {
                WebsocketY.SendAsync(data).Wait();
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        public void Send(string messageB) {
            try {
                WebsocketY.SendAsync(messageB).Wait();
            } catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
