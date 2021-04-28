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

        private static string modulename = "staticimage";
        private Image imgScreenPreview;
        private IWebSocketConnection serverB;

        private KLC.LiveConnectSession session;
        System.Timers.Timer timerStart;
        System.Timers.Timer timerRefresh;
        private List<RCScreen> listScreen = new List<RCScreen>();
        private RCScreen currentScreen = null;
        private int requestWidth, requestHeight;

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
            Console.WriteLine("I was not expecting this!");
        }

        public void HandleBytes(byte[] bytes) {
            byte type = bytes[0];

            if (type == 0x27) {
                Console.WriteLine("Connected StaticImage?");
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
                    Console.WriteLine("StaticImage - HostDesktopConfiguration");
                    //Console.WriteLine(jsonstr);

                    int default_screen = (int)json["default_screen"];
                    //Console.WriteLine("StaticImage - Clear Screens");
                    ClearScreens();

                    foreach (dynamic screen in json["screens"]) {
                        string screen_id = screen["screen_id"].ToString(); //int or BigInteger
                        string screen_name = (string)screen["screen_name"];
                        int screen_height = (int)screen["screen_height"];
                        int screen_width = (int)screen["screen_width"];
                        int screen_x = (int)screen["screen_x"];
                        int screen_y = (int)screen["screen_y"];

                        AddScreen(screen_id, screen_name, screen_height, screen_width, screen_x, screen_y);
                        Console.WriteLine("StaticImage - Add Screen: " + screen_id);

                        if(screen["screen_id"].ToString() == json["default_screen"].ToString()) { //int or BigInteger
                            //Same as how it's done in Kaseya's rc-screenshot.html
                            requestWidth = (int)Math.Ceiling(screen_width / 3.0);
                            requestHeight = (int)Math.Ceiling(screen_height / 3.0);
                        }
                    }

                    RequestRefresh();
                    timerRefresh.Start();
                } else if(type == (byte)Enums.KaseyaMessageTypes.ThumbnailResult) {
                    imgScreenPreview.Dispatcher.Invoke(new Action(() => {
                        using (MemoryStream stream = new MemoryStream(remaining)) {
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
                } else if (type == (byte)Enums.KaseyaMessageTypes.Clipboard) {
                    //Yes this is a thing...
                    Console.WriteLine("StaticImage - Ignoring clipboard event");
                } else {
                    Console.WriteLine("StaticImage - Unknown: " + type);
                    Console.WriteLine(jsonstr);

                    if (remStart < 0 || remLength < 1)
                        Console.WriteLine("Start: " + remStart + " - Length: " + remLength);
                    else {
                        string remainingHex = BitConverter.ToString(bytes, remStart, remLength).Replace("-", "");
                        string remainingstr = Encoding.UTF8.GetString(bytes, 5 + jsonLength, remLength);
                        Console.WriteLine("StaticImage - Remaining: " + remainingHex);
                    }
                }
            }
        }

        private void TimerRefresh_Elapsed(object sender, System.Timers.ElapsedEventArgs e) {
            RequestRefresh();
        }

        public void RequestRefresh() {
            SendThumbnailRequest(requestWidth, requestHeight);
        }

        public void RequestRefreshFull() {
            if(currentScreen != null)
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
            int jsonLen = sendjson.Length;
            byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);

            byte[] tosend = new byte[jsonLen + 5];
            tosend[0] = (byte)messageType;
            tosend[4] = (byte)jsonLen;
            Array.Copy(jsonBuffer, 0, tosend, 5, jsonLen);

            if (serverB != null) {
                if (serverB.IsAvailable)
                    serverB.Send(tosend);
                else {
                    imgScreenPreview.Dispatcher.Invoke(new Action(() => {
                        imgScreenPreview.Source = null;
                    }));
                }
            }
        }

        private void SendThumbnailRequest(int width, int height) {
            if (width > 0 && height > 0) {
                string sendjson = "{\"width\":" + width + ",\"height\":" + height + "}";
                SendJson(Enums.KaseyaMessageTypes.ThumbnailRequest, sendjson);
            }
        }

    }
}