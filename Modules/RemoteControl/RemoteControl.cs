using Fleck;
using LibKaseya;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Timers;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using static LibKaseya.Enums;

namespace KLC_Finch {

    public class RemoteControl : IRemoteControl {
        public WindowViewerV3 Viewer;
        private readonly RC mode;
        private readonly KLC.LiveConnectSession session;
        private bool captureScreen;
        private int clientB;

        //private string activeScreenID;
        private VP8.Decoder decoder;

        //private Vpx.Net.VP8Codec decoder2; //Test
        private ulong lastFrameAckSeq;

        private ulong lastFrameAckTimestamp;
        private bool mouseDown;
        private string rcSessionId;
        private IWebSocketConnection serverB;
        private System.Timers.Timer timerHeartbeat;
        private UploadRC fileUpload;

        public RemoteControl(KLC.LiveConnectSession session, RC mode) {
            this.session = session;
            this.mode = mode;

            decoder = new VP8.Decoder();
            //decoder2 = new Vpx.Net.VP8Codec(); //Test
        }

        //public WindowViewerV2 Viewer;
        //public bool UseYUVShader { get; set; }
        public DecodeMode DecodeMode { get; set; }

        public void CaptureNextScreen() {
            captureScreen = true;
        }

        public void ChangeScreen(string screen_id) {
            string sendjson = "{\"monitorId\":" + screen_id + "}"; //Intentionally using a string as int/biginteger
            SendJson(Enums.KaseyaMessageTypes.UpdateMonitorId, sendjson);
            //activeScreenID = screen_id;
        }

        public void ChangeTSSession(string session_id) {
            string sendjson = "{\"sessionId\":" + session_id + "}"; //Intentionally using a string as int/biginteger
            SendJson(Enums.KaseyaMessageTypes.UpdateTerminalSessionId, sendjson);
        }

        public void CloseViewer() {
            Disconnect(rcSessionId);
            if (Viewer != null)
                Viewer.Close();
        }

        public void Connect() {
            if (App.viewer != null) {
                App.viewer.Close();
                App.viewer = null;
            }

            Viewer = App.viewer = new WindowViewerV3(App.Settings.Renderer, this, session.agent.OSTypeProfile, session.agent.UserLast);
            Viewer.SetTitle(session.agent.Name, mode);
            Viewer.SetApprovalAndSpecialNote(session.RCNotify, session.agent.MachineShowToolTip, session.agent.MachineNote, session.agent.MachineNoteLink);
            Viewer.Show();

            if (session != null) {
                rcSessionId = session.WebsocketB.StartModuleRemoteControl(mode);
                Viewer.SetSessionID(rcSessionId);
            }

            if (rcSessionId != null) {
                timerHeartbeat = new System.Timers.Timer(1000);
                timerHeartbeat.Elapsed += SendHeartbeat;
                timerHeartbeat.Start();
            }
        }

        public void Disconnect(string sessionId) {
            if (decoder != null) {
                lock (decoder) {
                    decoder.Dispose();
                    decoder = null;
                }
            }

            if (timerHeartbeat != null)
                timerHeartbeat.Stop();
            if (serverB != null)
                serverB.Close();
            if (Viewer != null)
                Viewer.NotifySocketClosed(sessionId);
        }

        public unsafe void HandleBytesFromRC(byte[] bytes) {
            byte type = bytes[0];

            if (type == 0x27) {
                Viewer.ClearApproval();

                /*
                //This does not appear to fix the API logs (as opposed to VSA logs) issue
                RestClient K_Client = new RestClient("https://vsa-web.company.com.au");
                RestRequest request = new RestRequest("api/v1.0/assetmgmt/agent/" + session.agentGuid + "/KLCAuditLogEntry", Method.PUT);
                request.AddHeader("Authorization", "Bearer " + session.shorttoken);
                request.AddParameter("Content-Type", "application/json");
                request.AddJsonBody("{\"UserName\":\"" + session.auth.UserName + "\",\"AgentName\":\"" + session.agent.Name + "\",\"LogMessage\":\"Remote Control Log Notes: \"}");
                IRestResponse response = K_Client.Execute(request);
                */
            } else if (type == (byte)Enums.KaseyaMessageTypes.SessionNotSupported) {
                App.ShowUnhandledExceptionFromSrc("SessionNotSupported", "Remote Control");
                Disconnect(rcSessionId);
                //Viewer.NotifySocketClosed(rcSessionId);
                //serverB.Close();
                //Private session failed, this is supposed to prompt the user if they want to use a Shared session
            } else if (type == (byte)Enums.KaseyaMessageTypes.PrivateSessionDisconnected) {
                //Console.WriteLine("PrivateSessionDisconnected");

                Disconnect(rcSessionId);
                //Viewer.NotifySocketClosed(rcSessionId);
                //serverB.Close();
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
                    //Console.WriteLine("HostDesktopConfiguration");

                    //Shared Jason
                    //{"default_screen":65539,"screens":[
                    //{"screen_height":1080,"screen_id":65539,"screen_name":"\\\\.\\DISPLAY1","screen_width":1920,"screen_x":1920,"screen_y":0},
                    //{"screen_height":1080,"screen_id":65537,"screen_name":"\\\\.\\DISPLAY2","screen_width":1920,"screen_x":0,"screen_y":0},
                    //{"screen_height":1080,"screen_id":65541,"screen_name":"\\\\.\\DISPLAY3","screen_width":1920,"screen_x":-1920,"screen_y":0}
                    //]}

                    //Private
                    //{"default_screen":65537,"screens":[{"screen_height":1080,"screen_id":65537,"screen_name":"\\\\.\\DISPLAY1","screen_width":1440,"screen_x":0,"screen_y":0}]}

                    Viewer.UpdateScreenLayout(json, jsonstr);
                    if (mode == RC.Shared)
                        Viewer.SetControlEnabled(true, true);
                    else
                        Viewer.SetControlEnabled(true, false);
                } else if (type == (byte)Enums.KaseyaMessageTypes.HostTerminalSessionsList) {
                    //Console.WriteLine("HostTerminalSessionsList");
                    //{"default_session":4294967294,"sessions":[{"session_id":4294967294,"session_name":"Console JH-TEST-2016\\Hackerman"},{"session_id":1,"session_name":"JH-TEST-2016\\TestUser"}]}

                    string default_session = json["default_session"].ToString();
                    Viewer.ClearTSSessions();
                    foreach (dynamic session in json["sessions"]) {
                        string session_id = session["session_id"].ToString(); //int or BigInteger
                        string session_name = (string)session["session_name"];

                        Viewer.AddTSSession(session_id, session_name);
                        //Console.WriteLine("Add TS Session: " + session_id);
                    }

                    //Viewer.SetActiveTSSession(default_session);
                } else if (type == (byte)Enums.KaseyaMessageTypes.CursorImage) {
                    //Only provided when the cursor image changes, can be from cher end user or remote controller.
                    //{"height":32,"hotspotX":0,"hotspotY":0,"screenPosX":433,"screenPosY":921,"width":32}

                    if (Viewer.GetStatePowerSaving())
                        return;

                    int cursorX = (int)json["screenPosX"];
                    int cursorY = (int)json["screenPosY"];
                    int cursorWidth = (int)json["width"];
                    int cursorHeight = (int)json["height"];
                    int cursorHotspotX = (int)json["hotspotX"];
                    int cursorHotspotY = (int)json["hotspotY"];

                    //string hex = BitConverter.ToString(remaining).Replace("-", "");

                    //Console.WriteLine(jsonstr + " = " + remaining.Length);
                    //Console.WriteLine("Hotspot: " + cursorHotspotX + ", " + cursorHotspotY);
                    Viewer.LoadCursor(cursorX, cursorY, cursorWidth, cursorHeight, cursorHotspotX, cursorHotspotY, remaining);
                } else if (type == (byte)Enums.KaseyaMessageTypes.Ping) {
                    long receivedEpoch = (long)json["timestamp"];
                    Viewer.UpdateLatency(DateTimeOffset.Now.ToUnixTimeMilliseconds() - receivedEpoch);
                } else if (type == (byte)Enums.KaseyaMessageTypes.Video) {
                    //Just send it anyway, otherwise Kaseya gets sad and stops sending frames
                    //PostFrameAcknowledgementMessage((ulong)json["sequence_number"], (ulong)json["timestamp"]);
                    //Moved to after decode

                    if (DecodeMode == DecodeMode.BitmapRGB) {
                        Bitmap b1 = null;
                        try {
                            if (decoder == null)
                                decoder = new VP8.Decoder(); //Due to Soft Reconnect
                            lock (decoder) {
                                b1 = decoder.Decode(remaining, 0);
                            }
                        } catch (Exception ex) {
                            Console.WriteLine("RC VP8 decode error: " + ex.ToString());
                        } finally {
                            if (b1 != null && !Viewer.GetStatePowerSaving()) {
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
                    } else {
                        //YUV or Y

                        try {
                            int rawWidth = 0;
                            int rawHeight = 0;
                            int rawStride = 0;
                            byte[] rawYUV;
                            if (decoder == null)
                                decoder = new VP8.Decoder(); //Due to Soft Reconnect
                            lock (decoder) {
                                //This causes GC pressure
                                if (DecodeMode == DecodeMode.RawYUV || captureScreen)
                                    rawYUV = decoder.DecodeRaw(remaining, out rawWidth, out rawHeight, out rawStride);
                                else
                                    rawYUV = decoder.DecodeRawBW(remaining, out rawWidth, out rawHeight, out rawStride);
                            }

                            /*
                            if (!File.Exists("vp8test.bin")) {
                                FileStream fs = new FileStream("vp8test.bin", FileMode.Create);
                                fs.Write(rawYUV, 0, rawYUV.Length);
                                fs.Flush();
                                fs.Close();
                            }
                            */

                            if (rawWidth != 0 && rawHeight != 0 && !Viewer.GetStatePowerSaving()) {
                                Viewer.LoadTextureRaw(rawYUV, rawWidth, rawHeight, rawStride);

                                if (captureScreen) {
                                    captureScreen = false;

                                    Bitmap b1 = decoder.RawToBitmap(rawYUV, rawWidth, rawHeight, rawStride);

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
                            }
                        } catch (Exception ex) {
                            Console.WriteLine("RC VP8 decode error: " + ex.ToString());
                        }
                    }

                    PostFrameAcknowledgementMessage((ulong)json["sequence_number"], (ulong)json["timestamp"]);
                } else if (type == (byte)Enums.KaseyaMessageTypes.Clipboard) {
                    //string remainingHex = BitConverter.ToString(bytes, remStart, remLength).Replace("-", "");
                    string remainingstr = Encoding.UTF8.GetString(remaining); // bytes, 5 + jsonLength, remLength);
                    Viewer.ReceiveClipboard(remainingstr);
                    //Console.WriteLine("Clipboard: " + jsonstr + " // " + remainingstr);
                } else if (type == (byte)Enums.KaseyaMessageTypes.DropUploadLastChunk) {
                    //string remainingstr = Encoding.UTF8.GetString(remaining);
                    string chunkStr = (string)json["chunk"];
                    int chunk = 0;
                    if (chunkStr.Length > 0) {
                        chunk = int.Parse(chunkStr);

                        if (fileUpload.Chunk == chunk)
                            fileUpload.Chunk++;
                    }

                    JObject jInfo = new JObject {
                        ["chunk"] = fileUpload.Chunk.ToString(),
                        ["mime_type"] = "application/octet-stream",
                        ["name"] = fileUpload.fileName
                    };
                    string sendjson = jInfo.ToString();
                    if (chunkStr.Length == 0) {
                        //fileUpload.Open();
                        SendJson(Enums.KaseyaMessageTypes.DropUploadFileInfo, sendjson);
                    }

                    //--

                    byte[] contentBuffer = fileUpload.ReadBlock();
                    if (contentBuffer.Length == 0) {
                        SendJson(Enums.KaseyaMessageTypes.DropUploadFileComplete, sendjson);
                        if (fileUpload.showExplorer) {
                            //This appears to be optional if you want the file explorer to open, which will happen inside RDP.
                            SendJson(Enums.KaseyaMessageTypes.DropUploadComplete, "{}");
                        }
                        fileUpload.Close();
                        fileUpload = null;
                    } else {
                        byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);
                        int jsonLen = jsonBuffer.Length;
                        int totalLen = jsonLen + contentBuffer.Length;

                        byte[] tosend = new byte[totalLen + 5];
                        tosend[0] = (byte)Enums.KaseyaMessageTypes.DropUploadFileChunk;
                        byte[] tosendPrefix = BitConverter.GetBytes(jsonLen).Reverse().ToArray();
                        Array.Copy(tosendPrefix, 0, tosend, 1, tosendPrefix.Length);
                        Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);
                        Array.Copy(contentBuffer, 0, tosend, 5 + jsonLen, contentBuffer.Length);

                        if (serverB != null && serverB.IsAvailable) {
                            serverB.Send(tosend);
                            //Console.WriteLine("UploadLastChunk: " + jsonstr);
                        }
                    }
                } else {
                    Console.WriteLine("Unhandled message type: " + type);
                }
            }
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

        public void SendAutotype(string text) {
            SendAutotype(text, 0);
        }

        public void SendAutotype(string text, int speedPreset) {
            //Finch/Hawk method
            if (session.agent.OSTypeProfile == Agent.OSProfile.Mac) {
                //speedPreset = 2;
                //MITM.SendText(serverB, text, speedPreset);
                SendPasteClipboard(text);
            } else {
                MITM.SendText(serverB, text, speedPreset);
            }
        }

        public void SendBlackScreenBlockInput(bool blackOutScreen, bool blockMouseKB) {
            //Kaseya method, 9.5.7817.8820
            //{"black_out_screen":0,"block_mouse_keyboard":1}

            JObject json = new JObject {
                ["black_out_screen"] = (blackOutScreen ? 1 : 0), //This doesn't seem to work yet
                ["block_mouse_keyboard"] = (blockMouseKB ? 1 : 2)
            };
            SendJson(Enums.KaseyaMessageTypes.BlackScreenBlockInput, json.ToString());
        }

        public void SendClipboard(string content) {
            string sendjson = "{\"mime_type\":\"UTF-8\"}";
            byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);
            byte[] contentBuffer = System.Text.Encoding.UTF8.GetBytes(content);

            int jsonLen = jsonBuffer.Length;
            int totalLen = jsonLen + contentBuffer.Length;

            byte[] tosend = new byte[totalLen + 5];
            tosend[0] = (byte)Enums.KaseyaMessageTypes.Clipboard;
            byte[] tosendPrefix = BitConverter.GetBytes(jsonLen).Reverse().ToArray();
            Array.Copy(tosendPrefix, 0, tosend, 1, tosendPrefix.Length);
            Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);
            Array.Copy(contentBuffer, 0, tosend, 5 + jsonLen, contentBuffer.Length);

            if (serverB != null && serverB.IsAvailable)
                serverB.Send(tosend);
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

        public void SendMouseDown(MouseButtons button) {
            //WinForms
            if (mouseDown)
                return;

            //0200000029
            //EXo* @T¤T¯ÊJËÌR0P'Fs}.){"button":1,"button_down":true,"type":0}

            int buttonID = (button == MouseButtons.Right ? 3 : 1);
            string sendjson = "{\"button\":" + buttonID + ",\"button_down\":true,\"type\":0}";
            SendJson(Enums.KaseyaMessageTypes.Mouse, sendjson);

            mouseDown = true;
        }

        public void SendMouseDown(MouseButton button) {
            //WPF
            if (mouseDown)
                return;

            int buttonID = (button == MouseButton.Right ? 3 : 1);
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

        public void SendMouseUp(MouseButtons button) {
            //WinForms
            if (!mouseDown)
                return;

            //0200000029
            //EXo* @T¤T¯ÊJËÌR0P'Fs}.){"button":1,"button_down":false,"type":0}

            int buttonID = (button == MouseButtons.Right ? 3 : 1);
            string sendjson = "{\"button\":" + buttonID + ",\"button_down\":false,\"type\":0}";
            SendJson(Enums.KaseyaMessageTypes.Mouse, sendjson);

            mouseDown = false;
        }

        public void SendMouseUp(MouseButton button) {
            //WPF
            if (!mouseDown)
                return;

            //0200000029
            //EXo* @T¤T¯ÊJËÌR0P'Fs}.){"button":1,"button_down":false,"type":0}

            int buttonID = (button == MouseButton.Right ? 3 : 1);
            string sendjson = "{\"button\":" + buttonID + ",\"button_down\":false,\"type\":0}";
            SendJson(Enums.KaseyaMessageTypes.Mouse, sendjson);

            mouseDown = false;
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

        public void SendPanicKeyRelease() {
            if (serverB == null || !serverB.IsAvailable)
                return;

            foreach (int jskey in KeycodeV2.ModifiersJS) {
                KeycodeV2 key = KeycodeV2.List.Find(x => x.JavascriptKeyCode == jskey);
                serverB.Send(MITM.GetSendKey(key, false));
            }
        }

        public void SendPasteClipboard(string text) {
            //Kaseya method, 9.5.7765.16499
            //Has a limit of 255 characters, apparently.
            JObject json = new JObject {
                ["content_to_paste"] = text
            };
            SendJson(Enums.KaseyaMessageTypes.PasteClipboard, json.ToString());
        }

        public void SendSecureAttentionSequence() {
            //Control+Alt+Delete
            //This doesn't seem to work in private sessions

            SendJson(Enums.KaseyaMessageTypes.SecureAttentionSequence, "{}");
        }

        public void SetSocket(IWebSocketConnection ServerBsocket, int ipPort) {
            this.serverB = ServerBsocket;
            this.clientB = ipPort;
        }

        public void ShowCursor(bool enabled) {
            //Kaseya method, 9.5.7765.16499
            JObject json = new JObject {
                ["enabled"] = enabled
            };
            SendJson(Enums.KaseyaMessageTypes.ShowCursor, json.ToString());
        }

        public void UpdateScreens(string jsonstr) {
            //Do nothing
        }

        public void UploadDrop(string file, Progress<int> progress, bool showExplorer) {
            if (fileUpload == null) {
                fileUpload = new UploadRC(file, showExplorer, progress);
                bool ableToOpen = fileUpload.Open();
                if (ableToOpen)
                    SendJson(Enums.KaseyaMessageTypes.DropUploadStart, "{}");
                else {
                    if (progress != null)
                        ((IProgress<int>)progress).Report(100);
                    fileUpload = null;
                }
            }

            /* Before KLC added their own file uploading
             *
            Console.WriteLine("Upload dropped file: " + file);
            if (session.ModuleFileExplorer == null) {
                //Console.WriteLine("Spinning up no-UI File module");
                session.ModuleFileExplorer = new FileExplorer(session);
            }

            bool proceed = session.ModuleFileExplorer.IsUsable();
            if (!proceed) {
                //Terrible timeout
                for (int i = 0; i < 10; i++) {
                    Thread.Sleep(1000);
                    proceed = session.ModuleFileExplorer.IsUsable();
                    if (proceed)
                        break;
                }
            }

            if (proceed) {
                session.ModuleFileExplorer.Upload(file, progress);
            }
            */
        }

        private static int GetNewPort() {
            TcpListener tcpListener = new TcpListener(IPAddress.Parse("127.0.0.1"), 0);
            tcpListener.Start();
            int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
            tcpListener.Stop();

            return port;
        }

        private static int SwapEndianness(int value) {
            var b1 = (value >> 0) & 0xff;
            var b2 = (value >> 8) & 0xff;
            var b3 = (value >> 16) & 0xff;
            var b4 = (value >> 24) & 0xff;

            return b1 << 24 | b2 << 16 | b3 << 8 | b4 << 0;
        }

        private void PostFrameAcknowledgementMessage(ulong sequence_number, ulong timestamp) {
            if (timestamp > lastFrameAckTimestamp) {
                lastFrameAckSeq = sequence_number;
                lastFrameAckTimestamp = timestamp;

                string sendjson = "{\"last_processed_sequence_number\":" + lastFrameAckSeq + ",\"most_recent_timestamp\":" + lastFrameAckTimestamp + "}";
                SendJson(Enums.KaseyaMessageTypes.FrameAcknowledgement, sendjson);
            } else {
                //Console.WriteLine("Frame Ack out of order");
            }
        }

        private void SendHeartbeat(object sender, ElapsedEventArgs e) {
            if (serverB == null || !serverB.IsAvailable)
                return;

            string sendjson = "{\"timestamp\":" + DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString() + "}";
            SendJson(Enums.KaseyaMessageTypes.Ping, sendjson);
        }

        private void SendJson(Enums.KaseyaMessageTypes messageType, string sendjson) {
            byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);
            int jsonLen = jsonBuffer.Length;

            byte[] tosend = new byte[jsonLen + 5];
            tosend[0] = (byte)messageType;
            byte[] tosendPrefix = BitConverter.GetBytes(jsonLen).Reverse().ToArray();
            Array.Copy(tosendPrefix, 0, tosend, 1, tosendPrefix.Length);
            Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);

            if (serverB != null && serverB.IsAvailable)
                serverB.Send(tosend);
            else
                Disconnect(rcSessionId);
        }

        /*
        private void SendJsonASCII(Enums.KaseyaMessageTypes messageType, string sendjson) {
            byte[] jsonBuffer = System.Text.Encoding.ASCII.GetBytes(sendjson);
            int jsonLen = jsonBuffer.Length;

            byte[] tosend = new byte[jsonLen + 5];
            tosend[0] = (byte)messageType;
            byte[] tosendPrefix = BitConverter.GetBytes(jsonLen).Reverse().ToArray();
            Array.Copy(tosendPrefix, 0, tosend, 1, tosendPrefix.Length);
            Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);

            //if (messageType == Enums.KaseyaMessageTypes.PasteClipboard)
                //Console.WriteLine(BitConverter.ToString(tosend).Replace("-", ""));

            if (serverB != null && serverB.IsAvailable)
                serverB.Send(tosend);
            else
                Disconnect(rcSessionId);
        }
        */
    }
}