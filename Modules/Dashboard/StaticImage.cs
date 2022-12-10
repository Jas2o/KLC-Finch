using Fleck;
using LibKaseya;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NTR;
using RestSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace KLC_Finch {
    public class StaticImage {

        //private static readonly string modulename = "staticimage";
        private readonly Image imgScreenPreview;
        private IWebSocketConnection serverB;

        private readonly KLC.LiveConnectSession session;
        private readonly System.Timers.Timer timerStart;
        private readonly System.Timers.Timer timerRefresh;
        private readonly List<RCScreen> listScreen = new List<RCScreen>();
        private RCScreen currentScreen = null;
        private int requestWidth, requestHeight;

        private bool useReconnectHack; //This is used by Macs to get updated screen layout
        //private string jsonScreens;

        public StaticImage(KLC.LiveConnectSession session, Image imgScreenPreview) {
            this.session = session;
            this.imgScreenPreview = imgScreenPreview;

            timerStart = new System.Timers.Timer(1000);
            timerStart.Elapsed += TimerStart_Elapsed;
            timerStart.Start();

            timerRefresh = new System.Timers.Timer(30000);
            timerRefresh.Elapsed += TimerRefresh_Elapsed;
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        private void TimerStart_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            if (session == null) {
                timerStart.Stop();
            } else if(session.WebsocketB.ControlAgentIsReady()) {
                timerStart.Stop();
                session.WebsocketB.ControlAgentSendStaticImage(150, 300);
            }
        }

        public void Receive(string message) {
            Console.WriteLine("[StaticImage:Unexpected] " + message);
        }

        public void HandleBytes(byte[] bytes) {
            byte type = bytes[0];

            if (type == 0x27) {
#if DEBUG
                Console.WriteLine("Connected StaticImage?");
#endif
            } else {
                int jsonLength = BitConverter.ToInt32(bytes, 1).SwapEndianness();
                string jsonstr = Encoding.UTF8.GetString(bytes, 5, jsonLength);
                dynamic json = JsonConvert.DeserializeObject(jsonstr);

                int remStart = 5 + jsonLength;
                int remLength = bytes.Length - remStart;
                byte[] remaining = new byte[remLength];

                if (remLength > 0)
                    Array.Copy(bytes, remStart, remaining, 0, remLength);

                if (type == (byte)Enums.KaseyaMessageTypes.HostDesktopConfiguration) {
#if DEBUG
                    Console.WriteLine("StaticImage - HostDesktopConfiguration");
                    //Console.WriteLine(jsonstr);
#endif
                    //jsonScreens = jsonstr;
                    if (useReconnectHack)
                    {
                        useReconnectHack = false;
                        //session.ModuleRemoteControl.state.UpdateScreenLayout(json);
                        session.ModuleRemoteControl.Viewer.UpdateScreenLayout(jsonstr);

                        if (session.ModuleRemoteControl.mode == Enums.RC.Shared)
                            session.ModuleRemoteControl.Viewer.SetControlEnabled(true, true);
                        else
                            session.ModuleRemoteControl.Viewer.SetControlEnabled(true, false);
                    }
                    string default_screen = json["default_screen"].ToString();
                    ClearScreens();

                    foreach (dynamic screen in json["screens"]) {
                        string screen_id = screen["screen_id"].ToString(); //int or BigInteger
                        string screen_name = (string)screen["screen_name"];
                        int screen_height = (int)screen["screen_height"];
                        int screen_width = (int)screen["screen_width"];
                        int screen_x = (int)screen["screen_x"];
                        int screen_y = (int)screen["screen_y"];

                        AddScreen(screen_id, screen_name, screen_height, screen_width, screen_x, screen_y);

                        if(screen["screen_id"].ToString() == default_screen) { //int or BigInteger
                            //Same as how it's done in Kaseya's rc-screenshot.html
                            requestWidth = (int)Math.Ceiling(screen_width / 3.0);
                            requestHeight = (int)Math.Ceiling(screen_height / 3.0);
                        }
                    }

                    RequestRefresh();
                    timerRefresh.Start();
                } else if(type == (byte)Enums.KaseyaMessageTypes.ThumbnailResult) {
                    if (imgScreenPreview != null)
                    {
                        //Could be null if using reconnect hack without a WindowAlternative
                        imgScreenPreview.Dispatcher.Invoke(new Action(() =>
                        {
                            using (MemoryStream stream = new MemoryStream(remaining))
                            {
                                BitmapImage bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.StreamSource = stream;
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                //Mac images have red/blue swapped but can't find a quick way to toggle
                                bitmap.EndInit();
                                bitmap.Freeze();

                                imgScreenPreview.Source = bitmap;
                            }
                        }));
                    }
                } else if (type == (byte)Enums.KaseyaMessageTypes.Clipboard) {
                    //Yes this is a thing...
#if DEBUG
                    Console.WriteLine("StaticImage - Ignoring clipboard event");
#endif
                } else {
#if DEBUG
                    Console.WriteLine("StaticImage - Unknown: " + type);
                    Console.WriteLine(jsonstr);

                    if (remStart < 0 || remLength < 1)
                        Console.WriteLine("Start: " + remStart + " - Length: " + remLength);
                    else {
                        string remainingHex = BitConverter.ToString(bytes, remStart, remLength).Replace("-", "");
                        string remainingstr = Encoding.UTF8.GetString(bytes, 5 + jsonLength, remLength);
                        Console.WriteLine("StaticImage - Remaining: " + remainingHex);
                    }
#endif
                }
            }
        }

        private void TimerRefresh_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            RequestRefresh();
        }

        public void RequestRefresh() {
            timerRefresh.Stop();
            timerRefresh.Start();

            SendThumbnailRequest(requestWidth, requestHeight);
        }

        public void RequestRefreshFull() {
            timerRefresh.Stop();
            timerRefresh.Start();

            if (currentScreen != null)
                SendThumbnailRequest(currentScreen.rect.Width, currentScreen.rect.Height);
        }

        public void ClearScreens() {
            listScreen.Clear();
            currentScreen = null;
        }

        public void AddScreen(string screen_id, string screen_name, int screen_height, int screen_width, int screen_x, int screen_y) {
            RCScreen newScreen = new RCScreen(screen_id, screen_name, screen_height, screen_width, screen_x, screen_y);
            listScreen.Add(newScreen);
            if (currentScreen == null) {
                currentScreen = newScreen;
            }
        }

        private void SendJson(Enums.KaseyaMessageTypes messageType, string sendjson) {
            byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);
            int jsonLen = jsonBuffer.Length;

            byte[] tosend = new byte[jsonLen + 5];
            tosend[0] = (byte)messageType;
            byte[] tosendPrefix = BitConverter.GetBytes(jsonLen).Reverse().ToArray();
            Array.Copy(tosendPrefix, 0, tosend, 1, tosendPrefix.Length);
            Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);

            if (serverB != null) {
                if (serverB.IsAvailable)
                    serverB.Send(tosend);
                /*
                else {
                    imgScreenPreview.Dispatcher.Invoke(new Action(() => {
                        imgScreenPreview.Source = null;
                    }));
                }
                */
            }
        }

        private void SendThumbnailRequest(int width, int height) {
            if (width > 0 && height > 0) {
                string sendjson = "{\"width\":" + width + ",\"height\":" + height + "}";
                SendJson(Enums.KaseyaMessageTypes.ThumbnailRequest, sendjson);
            }
        }

        public string DumpScreens()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("StaticImage info:");
            bool more = false;
            foreach (RCScreen screen in listScreen)
            {
                if (more)
                    sb.Append(",");

                sb.AppendLine('{' + string.Format("\"screen_height\":{0},\"screen_id\":{1},\"screen_name\":\"{2}\",\"screen_width\":{3},\"screen_x\":{4},\"screen_y\":{5}", screen.rect.Height, screen.screen_id, screen.screen_name.Replace("\\", "\\\\"), screen.rect.Width, screen.rect.X, screen.rect.Y) + '}');

                more = true;
            }

            return sb.ToString();
        }

        public void ReconnectHack()
        {
            useReconnectHack = true;
            if (serverB != null && serverB.IsAvailable)
                serverB.Close();
            session.WebsocketB.ControlAgentSendStaticImage(150, 300);
        }

    }
}