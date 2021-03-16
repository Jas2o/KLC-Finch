﻿using Fleck;
using LibKaseya;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KLC_Finch {
    public class RemoteControl {

        public WindowViewer Viewer;
        private KLC.LiveConnectSession session;
        private string rcSessionId;
        System.Timers.Timer timerHeartbeat;
        private bool modePrivate;
        private VP8.Decoder decoder;

        private IWebSocketConnection serverB;
        private int clientB;

        private bool mouseDown;
        private bool captureScreen;

        public RemoteControl(KLC.LiveConnectSession session, bool modePrivate) {
            this.session = session;
            this.modePrivate = modePrivate;

            decoder = new VP8.Decoder();
        }

        public void Connect() {
            if (App.viewer != null) {
                App.viewer.Close();
                App.viewer = null;
            }

            Viewer = App.viewer = new WindowViewer(this, 1920, 1080);
            Viewer.SetTitle(session.agent.Name + "::" + (modePrivate ? "Private" : "Shared"));
            Viewer.Show();

            if (session != null) {
                rcSessionId = session.WebsocketB.StartModuleRemoteControl(modePrivate);
                Viewer.SetSessionID(rcSessionId);
            }

            if (rcSessionId != null) {
                timerHeartbeat = new System.Timers.Timer(1000);
                timerHeartbeat.Elapsed += SendHeartbeat;
                timerHeartbeat.Start();
            }
        }

        public void SetSocket(IWebSocketConnection ServerBsocket, int ipPort) {
            this.serverB = ServerBsocket;
            this.clientB = ipPort;
        }

        public void Reconnect() {
            Visibility visBefore = App.alternative.Visibility;
            App.alternative.Visibility = Visibility.Visible;

            WindowState tempState = Viewer.WindowState;
            Viewer.WindowState = WindowState.Normal;
            int tempLeft = (int)Viewer.Left;
            int tempTop = (int)Viewer.Top;
            int tempWidth = (int)Viewer.Width;
            int tempHeight = (int)Viewer.Height;

            Connect();

            Viewer.Left = tempLeft;
            Viewer.Top = tempTop;
            Viewer.Width = tempWidth;
            Viewer.Height = tempHeight;
            Viewer.WindowState = tempState;

            App.alternative.Visibility = visBefore;
        }

        public void CloseViewer() {
            Disconnect(rcSessionId);
            if(Viewer != null)
                Viewer.Close();
        }

        public void Disconnect(string sessionId) {
            if(timerHeartbeat != null)
                timerHeartbeat.Stop();
            if (serverB != null)
                serverB.Close();
            if(Viewer != null)
                Viewer.NotifySocketClosed(sessionId);
        }

        private void SendHeartbeat(object sender, ElapsedEventArgs e) {
            if (!serverB.IsAvailable)
                return;

                string sendjson = "{\"timestamp\":" + DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() + "}";
            SendJson(Enums.KaseyaMessageTypes.Ping, sendjson);
        }

        private static int GetNewPort() {
            TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 0);
            tcpListener.Start();
            int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            return port;
        }

        public unsafe void HandleBytesFromRC(byte[] bytes) {
            byte type = bytes[0];

            if (type == 0x27) {
                Console.WriteLine("Connected RC?");
            } else if (type == (byte)Enums.KaseyaMessageTypes.SessionNotSupported) {
                Console.WriteLine("SessionNotSupported");
                //Private session failed, this is supposed to prompt the user if they want to use a Shared session
            } else if (type == (byte)Enums.KaseyaMessageTypes.PrivateSessionDisconnected) {
                Console.WriteLine("PrivateSessionDisconnected");
                Viewer.NotifySocketClosed(rcSessionId);
                serverB.Close();
                //serverB.DisconnectClient(clientB);
            } else {
                int jsonLength = BitConverter.ToInt32(bytes, 1).SwapEndianness();
                string jsonstr = Encoding.UTF8.GetString(bytes, 5, jsonLength);
                dynamic json = JsonConvert.DeserializeObject(jsonstr);

                int remStart = 5 + jsonLength;
                int remLength = bytes.Length - remStart;
                byte[] remaining = new byte[remLength];

                //Console.WriteLine(jsonstr);
                if (remLength > 0) {
                    Array.Copy(bytes, remStart, remaining, 0, remLength);
                    //string remainingHex = BitConverter.ToString(bytes, remStart, remLength).Replace("-", "");
                    //string remainingstr = Encoding.UTF8.GetString(bytes, 5 + jsonLength, remLength);
                    //Console.WriteLine(remainingHex);
                }

                if (type == (byte)Enums.KaseyaMessageTypes.HostDesktopConfiguration) {
                    Console.WriteLine("HostDesktopConfiguration");

                    //Shared Jason
                    //{"default_screen":65539,"screens":[
                    //{"screen_height":1080,"screen_id":65539,"screen_name":"\\\\.\\DISPLAY1","screen_width":1920,"screen_x":1920,"screen_y":0},
                    //{"screen_height":1080,"screen_id":65537,"screen_name":"\\\\.\\DISPLAY2","screen_width":1920,"screen_x":0,"screen_y":0},
                    //{"screen_height":1080,"screen_id":65541,"screen_name":"\\\\.\\DISPLAY3","screen_width":1920,"screen_x":-1920,"screen_y":0}
                    //]}

                    //Private
                    //{"default_screen":65537,"screens":[{"screen_height":1080,"screen_id":65537,"screen_name":"\\\\.\\DISPLAY1","screen_width":1440,"screen_x":0,"screen_y":0}]}

                    int default_screen = (int)json["default_screen"];
                    Console.WriteLine("Clear Screens");
                    Viewer.ClearScreens();
                    foreach (dynamic screen in json["screens"]) {

                        string screen_id = screen["screen_id"].ToString(); //int or BigInteger
                        string screen_name = (string)screen["screen_name"];
                        int screen_height = (int)screen["screen_height"];
                        int screen_width = (int)screen["screen_width"];
                        int screen_x = (int)screen["screen_x"];
                        int screen_y = (int)screen["screen_y"];

                        Viewer.AddScreen(screen_id, screen_name, screen_height, screen_width, screen_x, screen_y);
                        Console.WriteLine("Add Screen: " + screen_id);

                        /*
                        if (screen_x == 0 && threadOpenTK == null) {

                            viewer.SetVirtual(screen_width, screen_height);

                            if (!session.agent.IsMac) {
                                //This is a hack to assume my middle screen is my main screen)
                                ChangeScreen(screen_id);
                            }

                            while (viewer == null) { Thread.Sleep(10); }
                        }
                        */
                    }

                    Viewer.SetControlEnabled(true);
                } else if (type == (byte)Enums.KaseyaMessageTypes.CursorImage) {
                } else if (type == (byte)Enums.KaseyaMessageTypes.Ping) {
                    long receivedEpoch = (long)json["timestamp"];
                    Viewer.UpdateLatency(DateTimeOffset.Now.ToUnixTimeMilliseconds() - receivedEpoch);
                } else if (type == (byte)Enums.KaseyaMessageTypes.Video) {

                    //Just send it anyway, otherwise Kaseya gets sad and stops sending frames
                    postFrameAcknowledgementMessage((ulong)json["sequence_number"], (ulong)json["timestamp"]);

                    /*
                    bool isKeyframe = false;
                    if (remaining[3] == 0x9D && remaining[4] == 0x01 && remaining[5] == 0x2A) {
                        isKeyframe = true;
                    }
                    //int width = json["width"];
                    //int height = json["height"];
                    */
                    Bitmap b1 = null;
                    try {
                        b1 = decoder.Decode(remaining);
                    } catch (Exception ex) {
                        Console.WriteLine("RC VP8 decode error: " + ex.ToString());
                    } finally {
                        if (b1 != null) {
                            Viewer.LoadTexture(b1.Width, b1.Height, b1);

                            if (captureScreen) {
                                captureScreen = false;

                                //This may cause AV to think we're malware
                                Thread tc = new Thread(() => {
                                    System.Windows.Forms.Clipboard.SetImage(b1);
                                });
                                tc.SetApartmentState(ApartmentState.STA);
                                tc.Start();
                                Thread.Sleep(1);
                                tc.Join();
                                Thread.Sleep(1);
                            }

                            b1.Dispose();
                        }
                    }

                } else if (type == (byte)Enums.KaseyaMessageTypes.Clipboard) {
                    //string remainingHex = BitConverter.ToString(bytes, remStart, remLength).Replace("-", "");
                    string remainingstr = Encoding.UTF8.GetString(remaining); // bytes, 5 + jsonLength, remLength);
                    Viewer.ReceiveClipboard(remainingstr);
                    Console.WriteLine("Clipboard: " + jsonstr + " // " + remainingstr);
                } else {
                    Console.WriteLine("Unhandled message type: " + type);
                }
            }
        }

        public void SendClipboard(string content) {
            string sendjson = "{\"mime_type\":\"UTF-8\"}";
            int jsonLen = sendjson.Length;
            int totalLen = jsonLen + content.Length;
            byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);
            byte[] contentBuffer = System.Text.Encoding.UTF8.GetBytes(content);

            byte[] tosend = new byte[totalLen + 5];
            tosend[0] = (byte)Enums.KaseyaMessageTypes.Clipboard;
            tosend[4] = (byte)jsonLen;
            Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);
            Array.Copy(contentBuffer, 0, tosend, 5 + jsonLen, content.Length);

            if (serverB != null)
                serverB.Send(tosend);
        }

        public void SendMouseUp(MouseButtons button) {
            if (!mouseDown)
                return;

            //0200000029
            //EXo* @T¤T¯ÊJËÌR0P'Fs}.){"button":1,"button_down":false,"type":0}

            int buttonID = (button == MouseButtons.Right ? 3 : 1);
            string sendjson = "{\"button\":" + buttonID + ",\"button_down\":false,\"type\":0}";
            SendJson(Enums.KaseyaMessageTypes.Mouse, sendjson);

            mouseDown = false;
        }

        public void SendMouseDown(MouseButtons button) {
            if (mouseDown)
                return;

            //0200000029
            //EXo* @T¤T¯ÊJËÌR0P'Fs}.){"button":1,"button_down":true,"type":0}

            int buttonID = (button == MouseButtons.Right ? 3 : 1);
            string sendjson = "{\"button\":" + buttonID + ",\"button_down\":true,\"type\":0}";
            SendJson(Enums.KaseyaMessageTypes.Mouse, sendjson);

            mouseDown = true;
        }

        public void SendMousePosition(int x, int y) {
            //8220020000001b
            //EJm@T¤T¯Ê(ªÌ/QP'ia {"type":1,"x":1799,"y":657}

            string sendjson = "{\"type\":1,\"x\":" + x + ",\"y\":" + y + "}";
            SendJson(Enums.KaseyaMessageTypes.Mouse, sendjson);
        }

        public void SendMouseWheel(int delta) {
            //8220020000001b
            //EJm@T¤T¯Ê(ªÌ/QP'ia {"type":1,"x":1799,"y":657}

            int deltaX = 0;
            int deltaY = delta;

            /*
            wheel_delta_x: e.deltaX ? e.deltaX : 0,
            wheel_delta_y: e.deltaY ? e.deltaY : 0,
            wheel_ticks_x: e.deltaX ? e.deltaX : 0,
            wheel_ticks_y: e.deltaY ? e.deltaY : 0
            */

            string sendjson = "{\"type\":2,\"wheel_delta_x\":" + deltaX + ",\"wheel_delta_y\":" + deltaY + ",\"wheel_ticks_x\":" + deltaX + ",\"wheel_ticks_y\":" + deltaY + "}";
            SendJson(Enums.KaseyaMessageTypes.Mouse, sendjson);
        }

        private static int SwapEndianness(int value) {
            var b1 = (value >> 0) & 0xff;
            var b2 = (value >> 8) & 0xff;
            var b3 = (value >> 16) & 0xff;
            var b4 = (value >> 24) & 0xff;

            return b1 << 24 | b2 << 16 | b3 << 8 | b4 << 0;
        }

        public void SendKeyDown(int KaseyaKeycode, int USBKeycode) {
            //8220020000001b
            //0300000081
            //E²}Â@,7 ÐyJ¾ P'Fu	~{"keyboard_layout_handle":"0","keyboard_layout_local":false,"lock_states":2,"pressed":true,"usb_keycode":458775,"virtual_key":84}

            string sendjson = "{\"keyboard_layout_handle\":\"0\",\"keyboard_layout_local\":false,\"lock_states\":2,\"pressed\":true,\"usb_keycode\":" + USBKeycode + ",\"virtual_key\":" + KaseyaKeycode + "}";
            SendJson(Enums.KaseyaMessageTypes.Keyboard, sendjson);
        }

        public void SendKeyUp(int KaseyaKeycode, int USBKeycode) {
            //8220020000001b
            //0300000081
            //E²}Â@,7 ÐyJ¾ P'Fu	~{"keyboard_layout_handle":"0","keyboard_layout_local":false,"lock_states":2,"pressed":true,"usb_keycode":458775,"virtual_key":84}

            string sendjson = "{\"keyboard_layout_handle\":\"0\",\"keyboard_layout_local\":false,\"lock_states\":2,\"pressed\":false,\"usb_keycode\":" + USBKeycode + ",\"virtual_key\":" + KaseyaKeycode + "}";
            SendJson(Enums.KaseyaMessageTypes.Keyboard, sendjson);
        }

        public void SendSecureAttentionSequence() {
            //Control+Alt+Delete
            //This doesn't seem to work in private sessions

            SendJson(Enums.KaseyaMessageTypes.SecureAttentionSequence, "{}");
        }

        public void ChangeScreen(string screen_id) {
            string sendjson = "{\"monitorId\":" + screen_id + "}"; //Intentionally using a string as int/biginteger
            SendJson(Enums.KaseyaMessageTypes.UpdateMonitorId, sendjson);
        }

        private void SendJson(Enums.KaseyaMessageTypes messageType, string sendjson) {
            int jsonLen = sendjson.Length;
            byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);

            byte[] tosend = new byte[jsonLen + 5];
            tosend[0] = (byte)messageType;
            tosend[4] = (byte)jsonLen;
            Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);

            if(serverB != null)
                serverB.Send(tosend);
        }


        private void _postRemoteControlMessage(dynamic e, dynamic t, dynamic s) {
            /*
            var o = {
                type: e
            };
            t && (o.fields = t),
            s && (o.bytes = s),
            1 === this._ws.readyState && this._ws.send(this._encodeRemoteControlMessage(o))
            */
        }

        private ulong lastFrameAckSeq;
        private ulong lastFrameAckTimestamp;
        private void postFrameAcknowledgementMessage(ulong sequence_number, ulong timestamp) {
            if (timestamp > lastFrameAckTimestamp) {
                lastFrameAckSeq = sequence_number;
                lastFrameAckTimestamp = timestamp;

                string sendjson = "{\"last_processed_sequence_number\":" + lastFrameAckSeq + ",\"most_recent_timestamp\":" + lastFrameAckTimestamp + "}";
                SendJson(Enums.KaseyaMessageTypes.FrameAcknowledgement, sendjson);
            } else {
                Console.WriteLine("Frame Ack out of order");
            }
        }

        public void SendAutotype(string text, int speedPreset=0) {
            if (session.agent.IsMac)
                speedPreset = 2;

            MITM.SendText(serverB, text, speedPreset);
        }

        public void SendPanicKeyRelease() {
            foreach (int jskey in KeycodeV2.ModifiersJS) {
                KeycodeV2 key = KeycodeV2.List.Find(x => x.JavascriptKeyCode == jskey);
                serverB.Send(MITM.GetSendKey(key, false));
            }
        }

        public void CaptureNextScreen() {
            captureScreen = true;
        }

        public void UploadDrop(string file) {
            Console.WriteLine("Upload dropped file: " + file);
            if (session.ModuleFileExplorer == null) {
                Console.WriteLine("Spinning up no-UI module");
                session.ModuleFileExplorer = new FileExplorer(session);
            }

            bool proceed = session.ModuleFileExplorer.IsUsable();
            if(!proceed) {
                //Terrible timeout
                for (int i = 0; i < 10; i++) {
                    Thread.Sleep(1000);
                    proceed = session.ModuleFileExplorer.IsUsable();
                    if (proceed)
                        break;
                }
            }

            if (proceed) {
                session.ModuleFileExplorer.Upload(file);
            }
        }
    }
}
