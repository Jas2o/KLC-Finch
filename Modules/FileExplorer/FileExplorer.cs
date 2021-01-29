using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Controls;

namespace KLC_Finch {
    public class FileExplorer {

        private static string modulename = "files";
        private IWebSocketConnection serverB, serverBdownload;

        private bool IsMac;
        private ListBox listExplorerFolders;
        private ListBox listExplorerFiles;
        private TextBox txtExplorerPath;
        private TextBox txtBox;

        private List<string> selectedPath;
        private Download queuedDownload;

        public FileExplorer(KLC.LiveConnectSession session, ListBox listExplorerFolders, ListBox listExplorerFiles, TextBox txtExplorerPath, TextBox txtBox = null) {
            this.listExplorerFolders = listExplorerFolders;
            this.listExplorerFiles = listExplorerFiles;
            this.txtExplorerPath = txtExplorerPath;
            this.txtBox = txtBox;

            selectedPath = new List<string>();

            if (session != null) {
                IsMac = session.agent.IsMac;
                session.WebsocketB.ControlAgentSendTask(modulename);
            }
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void SetDownloadSocket(IWebSocketConnection ServerBsocket) {
            this.serverBdownload = ServerBsocket;
        }

        public void Receive(string message) {
            txtBox.Dispatcher.Invoke(new Action(() => {
                dynamic temp = JsonConvert.DeserializeObject(message);
                switch ((string)temp["action"]) {
                    case "ScriptReady":
                        Update();
                        break;
                    case "GetDrives":
                        listExplorerFolders.Items.Clear();
                        listExplorerFiles.Items.Clear();

                        txtBox.Clear();
                        txtBox.AppendText(message + "\r\n");
                        if ((bool)temp["success"]) {
                            foreach (dynamic key in temp["contentsList"].Children())
                                listExplorerFolders.Items.Add(key["name"]);
                            // + " - Type: " + key["type"] + " - Size: " + key["size"] + " - Date: " + key["date"]

                            //foreach pathArray
                        }
                        break;
                    case "GetFolderContents":
                        listExplorerFolders.Items.Clear();
                        listExplorerFiles.Items.Clear();

                        txtBox.Clear();
                        txtBox.AppendText(message + "\r\n");
                        if ((bool)temp["success"]) {
                            foreach (dynamic key in temp["contentsList"].Children()) {

                                if ((string)key["type"] == "file") {
                                    listExplorerFiles.Items.Add((string)key["name"]);
                                    // + " - Size: " + key["size"] + " - Date: " + key["date"]
                                } else if ((string)key["type"] == "folder") {
                                    listExplorerFolders.Items.Add((string)key["name"]);
                                    // + " - Date: " + key["date"]
                                } else {
                                    listExplorerFiles.Items.Add("??? - " + (string)key["name"]);
                                    // + " - Type: " + key["type"] + " - Size: " + key["size"] + " - Date: " + key["date"]
                                }
                            }

                            selectedPath.Clear();
                            if (IsMac) {
                                foreach (dynamic key in temp["pathArray"].Children()) {
                                    selectedPath.Add(key.ToString());
                                }
                            } else {
                                foreach (dynamic key in temp["pathArray"].Children()) {
                                    selectedPath.Add(key.ToString().Replace("\\", "").Replace("/", ""));
                                }

                            }

                            //temp["id"] is the time the response was generated
                        }
                        break;
                    default:
                        txtBox.AppendText("FileExplorer message received: " + message + "\r\n");
                        break;
                }
            }));
        }

        public void HandleDownload(byte[] data) {
            int jsonLength = BitConverter.ToInt32(data, 0).SwapEndianness();
            string jsonstr = Encoding.UTF8.GetString(data, 4, jsonLength);
            dynamic json = JsonConvert.DeserializeObject(jsonstr);

            int remStart = 4 + jsonLength;
            int remLength = data.Length - remStart;
            byte[] remaining = new byte[remLength];
            if (remLength > 0)
                Array.Copy(data, remStart, remaining, 0, remLength);

            switch (json["action"].ToString()) {
                case "Ready":
                    //queuedDownload

                    JObject jDown = new JObject();

                    jDown["action"] = "Begin";
                    JArray jDownPath = new JArray();
                    for (int i = 0; i < queuedDownload.Path.Count; i++) {
                        if (i == 0) {
                            if (IsMac)
                                jDownPath.Add("/");
                            else
                                jDownPath.Add(selectedPath[i] + "\\");
                        } else
                            jDownPath.Add(selectedPath[i]);
                    }
                    jDown["path"] = jDownPath;
                    jDown["filename"] = queuedDownload.fileName;
                    jDown["type"] = "file";
                    jDown["fileId"] = queuedDownload.fileID;

                    //--

                    string sendjson = jDown.ToString();
                    int jsonLen = sendjson.Length;
                    int totalLen = jsonLen;// + content.Length;
                    byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);
                    //byte[] contentBuffer = System.Text.Encoding.UTF8.GetBytes(content);

                    byte[] tosend = new byte[totalLen + 4];
                    tosend[3] = (byte)jsonLen;
                    Array.Copy(jsonBuffer, 0, tosend, 4, jsonLen);
                    //Array.Copy(contentBuffer, 0, tosend, 4 + jsonLen, content.Length);

                    if (serverBdownload != null)
                        serverBdownload.Send(tosend);
                    break;

                case "Data":
                    //{"action":"Data","fileSize":663,"requiresAck":true}[Data]
                    queuedDownload.WriteData(remaining);

                    if (json["requiresAck"] != null) {
                        if ((bool)json["requiresAck"] == true) {
                            #region Ack
                            JObject jAck = new JObject();
                            jAck["action"] = "DataAck";
                            jAck["filename"] = queuedDownload.fileName;
                            string sendjsonAck = jAck.ToString();
                            int jsonLenAck = sendjsonAck.Length;
                            int totalLenAck = jsonLenAck;// + content.Length;
                            byte[] jsonBufferAck = System.Text.Encoding.UTF8.GetBytes(sendjsonAck);

                            byte[] tosendAck = new byte[totalLenAck + 4];
                            tosendAck[3] = (byte)jsonLenAck;
                            Array.Copy(jsonBufferAck, 0, tosendAck, 4, jsonLenAck);

                            Console.WriteLine("Sending ack");

                            if (serverBdownload != null)
                                serverBdownload.Send(tosendAck);
                        } else {
                            Console.WriteLine("No ack");
                        }
                    }
                    #endregion
                    break;

                case "End":
                    //{"action":"End","permissions":{"isReadOnly":false,"execPerms":{"owner":0,"group":0,"others":0}},"date":"2020-12-22T06:23:42.000Z"}
                    queuedDownload.Close();
                    serverBdownload.Close();

                    txtBox.Dispatcher.Invoke(new Action(() => {
                        txtBox.AppendText("\r\nDownload complete.");
                    }));
                    break;

                case "Error":
                    Console.WriteLine("Download failed");
                    queuedDownload.Close();
                    serverBdownload.Close();
                    break;

                default:
                    Console.WriteLine();
                    break;
            }
        }

        public void Download(string selectedFile, string saveFile) {
            long fileID = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            queuedDownload = new Download(selectedPath, selectedFile, saveFile, fileID, "file");

            JObject jDown = new JObject();

            jDown["action"] = "Download";
            JArray jDownPath = new JArray();
            for (int i = 0; i < selectedPath.Count; i++) {
                if (i == 0) {
                    if (IsMac)
                        jDownPath.Add("/");
                    else
                        jDownPath.Add(selectedPath[i] + "\\");
                } else
                    jDownPath.Add(selectedPath[i]);
            }
            jDown["path"] = jDownPath;
            jDown["filename"] = selectedFile;
            jDown["type"] = "file";
            jDown["fileId"] = fileID;
            jDown["id"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            serverB.Send(jDown.ToString());

            txtBox.Text = "Starting download: " + saveFile;
        }

        /*
        private WebSocket WS_EdgeRelay(string authPayloadjsonWebToken, string sessionId) {

            string pathModule = Util.EncodeToBase64("/app/" + modulename);

            WebSocket websocket = new WebSocket("wss://vsa-web.company.com.au:443/kaseya/edge/relay?auth=" + authPayloadjsonWebToken + "&relayId=" + sessionId + "|" + pathModule);

            websocket.AutoSendPingInterval = 5;
            websocket.EnableAutoSendPing = true;
            if (txtBox != null) {
                websocket.Opened += (sender, e) => txtBox.Invoke(new Action(() => {
                    txtBox.AppendText("FileExplorer Socket opened: " + sessionId + "\r\n");
                }));
                websocket.Closed += (sender, e) => txtBox.Invoke(new Action(() => {
                    txtBox.AppendText("FileExplorer Socket closed: " + sessionId + " - " + e.ToString() + "\r\n");
                }));
                websocket.MessageReceived += (sender, e) => 
                }));
                websocket.Error += (sender, e) => txtBox.Invoke(new Action(() => {
                    txtBox.AppendText("FileExplorer Socket error: " + sessionId + " - " + e.Exception.ToString() + "\r\n");
                }));
            } else {
                websocket.Opened += (sender, e) => Console.WriteLine("FileExplorer Socket opened: " + sessionId);
                websocket.Closed += (sender, e) => Console.WriteLine("FileExplorer Socket closed: " + sessionId + " - " + e.ToString());
                websocket.MessageReceived += (sender, e) => Console.WriteLine("FileExplorer message received: " + sessionId + " - " + e.Message);
                websocket.Error += (sender, e) => Console.WriteLine("FileExplorer Socket error: " + sessionId + " - " + e.Exception.ToString());
            }

            Util.ConfigureProxy(websocket);

            websocket.Open();
            return websocket;
        }
        */

        private void Update() {
            //{"action":"GetFolderContents","path":["C:\\","temp"],"id":1582283274052}
            JObject jGet = new JObject();

            if (IsMac) {
                if (selectedPath.Count == 0) {
                    txtExplorerPath.Text = "";
                } else if (selectedPath.Count == 1) {
                    if (selectedPath[0] == "")
                        selectedPath[0] = "/";
                    txtExplorerPath.Text = selectedPath[0];
                } else {
                    txtExplorerPath.Text = (string.Join("/", selectedPath) + "/").Replace("//", "/");
                }
            } else {
                if (selectedPath.Count > 0 && !selectedPath[0].Any(Char.IsLetter))
                    selectedPath.Clear();
                txtExplorerPath.Text = string.Join("\\", selectedPath) + "\\";
            }

            if (selectedPath.Count == 0) {
                jGet["action"] = "GetDrives";
            } else {
                jGet["action"] = "GetFolderContents";
                JArray jGetPath = new JArray();
                for (int i = 0; i < selectedPath.Count; i++) {
                    if (i == 0) {
                        if (IsMac)
                            jGetPath.Add("/");
                        else
                            jGetPath.Add(selectedPath[i] + "\\");
                    } else
                        jGetPath.Add(selectedPath[i]);
                }
                jGet["path"] = jGetPath;
                jGet["id"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }

            serverB.Send(jGet.ToString());
        }

        public void SelectPath(string key) {
            if (IsMac)
                selectedPath.Add(key);
            else
                selectedPath.Add(key.Replace("\\", "").Replace("/", ""));

            Update();
        }

        public void GoTo(string input) {
            if (IsMac) {
                if (input == "") {
                    selectedPath.Clear();
                } else {
                    selectedPath = input.Split(new char[] { '/' }).ToList(); //Backslashes are valid characters in Mac
                    for (int i = selectedPath.Count - 1; i > 0; i--) {
                        if (selectedPath[i] == "")
                            selectedPath.RemoveAt(i);
                    }
                }
            } else {
                selectedPath = input.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }

            Update();
        }

        public void GoUp() {
            if (selectedPath.Count > 0)
                selectedPath.RemoveAt(selectedPath.Count - 1);

            Update();
        }

        //{"action":"GetFolderContents","path":["C:\\"],"id":1582282539328}

        /*
         *  case "ScriptReady":
                this._getDrives();
                break;
            case "GetDrives":
            case "GetFolderContents":
                if (this.$.toolbar.breadcrumbsActive = !0,
                e.success) {
                    this._fileContent = e.contentsList;
                    var t = [liveconnect.getSession().asset.AgentName];
                    this._selectedPath = t.concat(e.pathArray),
                    this._copyFolderCallback ? (this.set("_copyFolderCallback", !1),
                    this._copyFolder()) : this._createBreadCrumb(this._selectedPath)
                } else
                    this._displayErrors(e.errorMessage);
                break;
            case "GetFullDownloadItemList":
                if (!e.success) {
                    this._displayErrors(e.errorMessage),
                    this._downloadsComplete();
                    break
                }
                for (var s = e.contentsList, i = e.pathArray, o = 0; o < s.length; o++)
                    if ("folder" === s[o].type) {
                        var n = this._downloads.reqInProgress.savePath + "/" + this._pathToString(s[o].name);
                        System.getDirectoryForPath(n).then(function(e) {}
                        .bind(this)).catch(function(e) {
                            log.error(e)
                        })
                    } else {
                        var r, a = this._downloads.reqInProgress.savePath + "/" + this._pathToString(s[o].name);
                        s[o].name.length > 1 ? (s[o].name.pop(),
                        r = i.concat(s[o].name)) : r = i;
                        var l = this._createPendingTransfer(a, r);
                        this._validateTransferToLocalPath(l)
                    }
                this.set("_downloads.reqInProgress", {}),
                this._processNextQueuedUserDownloadRequest();
                break;
            case "DeleteItem":
            case "CreateFolder":
            case "MakeACopy":
            case "MoveTo":
                this._updateContentsIfPath(e.pathArray, e.contentsList),
                e.success ? ("DeleteItem" === e.action ? this._successMessage = this.xlate("ITEMS_REMOVED") : "CreateFolder" === e.action ? this._successMessage = this.xlate("FOLDER_CREATED") : "MoveTo" === e.action ? this._successMessage = this.xlate("ITEMS_MOVED") : this._successMessage = this.xlate("ITEM_COPIED"),
                this._showSuccessToast && this.$.successToast.show()) : this._displayErrors(e.errorMessage);
                break;
            case "RenameItem":
                this._updateContentsIfPath(e.pathArray, e.contentsList),
                e.success ? (this._successMessage = this.xlate("ITEM_RENAMED"),
                this.$.successToast.show(),
                this._renameValue = void 0) : (this.set("_errorObject", e.errorMessage),
                this.$.renameErrorDialog.show());
                break;
            case "Upload":
                this._uploads.active && this._uploads.active.fileID === e.fileID && (e.success || (this._displayErrorsUpload(e.errorMessage),
                this._updateContentsIfPath(e.pathArray, e.contentsList),
                this._uploads.channel.close().then(this._uploadCompleteForActiveFile.bind(this))));
                break;
            case "Download":
                e.success || this._downloads.active.fileID === e.fileID && (this._displayErrors(e.errorMessage),
                this._downloads.channel.close().then(this._downloadCompleteForActiveFile.bind(this, !0)))
            }
        */
    }
}
