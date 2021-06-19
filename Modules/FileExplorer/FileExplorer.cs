﻿using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows.Controls;

namespace KLC_Finch {
    public class FileExplorer {

        private static string modulename = "files";
        private IWebSocketConnection serverB, serverBdownload, serverBupload;

        private Modules.FilesData filesData;

        private bool IsMac;
        private ListBox listExplorerFolders;
        //private DataGrid dgvExplorerFiles;
        private TextBox txtExplorerPath;
        //private TextBox txtBox;
        private ProgressBar progressBar;
        private TextBlock progressText;
        private Button btnDownload;
        private Button btnUpload;

        private List<string> selectedPath;
        //private List<KLCFile> viewFiles;
        private List<KLCFile> viewFolders;

        private Queue<Download> queueDownload;
        private Queue<Upload> queueUpload;
        private Download activeDownload;
        private Upload activeUpload;
        private string downloadDestination;

        public FileExplorer(KLC.LiveConnectSession session) {
            selectedPath = new List<string>();
            //viewFiles = new List<KLCFile>();
            viewFolders = new List<KLCFile>();

            queueDownload = new Queue<Download>();
            queueUpload = new Queue<Upload>();

            if (session != null) {
                IsMac = session.agent.IsMac;
                session.WebsocketB.ControlAgentSendTask(modulename);
            }
        }

        public void LinkToUI(ListBox listExplorerFolders, TextBox txtExplorerPath, ProgressBar progressBar = null, TextBlock progressText = null, Button btnDownload = null, Button btnUpload = null, Modules.FilesData filesData = null) {
            this.listExplorerFolders = listExplorerFolders;
            //this.dgvExplorerFiles = dgvExplorerFiles;
            this.txtExplorerPath = txtExplorerPath;
            //this.txtBox = txtBox;
            this.progressBar = progressBar;
            this.progressText = progressText;
            this.btnDownload = btnDownload;
            this.btnUpload = btnUpload;

            this.filesData = filesData;
        }

        public bool IsUsable() {
            return (serverB != null && serverB.IsAvailable);
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void SetDownloadSocket(IWebSocketConnection ServerBsocket) {
            this.serverBdownload = ServerBsocket;
        }

        public void SetUploadSocket(IWebSocketConnection ServerBsocket) {
            this.serverBupload = ServerBsocket;
        }

        public int GetSelectedPathLength() {
            return selectedPath.Count;
        }

        public void Receive(string message) {
            //txtBox.Dispatcher.Invoke(new Action(() => {
            dynamic temp = JsonConvert.DeserializeObject(message);
            switch ((string)temp["action"]) {
                case "ScriptReady":
                    if (txtExplorerPath != null) {
                        txtExplorerPath.Dispatcher.Invoke(new Action(() => {
                            Update();
                        }));
                    }
                    break;
                case "GetDrives":
                    if (txtExplorerPath != null) {
                        txtExplorerPath.Dispatcher.Invoke(new Action(() => {
                            listExplorerFolders.Items.Clear();
                            //dgvExplorerFiles.Items.Clear();
                            filesData.FilesClear();
                            if ((bool)temp["success"]) {
                                foreach (dynamic key in temp["contentsList"].Children())
                                    listExplorerFolders.Items.Add(key["name"]);
                                // + " - Type: " + key["type"] + " - Size: " + key["size"] + " - Date: " + key["date"]

                                //foreach pathArray
                            }
                        }));
                    }
                    break;
                case "CreateFolder":
                case "RenameItem":
                case "DeleteItem":
                case "GetFolderContents":
                    if (txtExplorerPath != null) {
                        txtExplorerPath.Dispatcher.Invoke(new Action(() => {
                            listExplorerFolders.Items.Clear();
                            //dgvExplorerFiles.Items.Clear();
                            filesData.FilesClear();
                            viewFolders.Clear();

                            //txtBox.AppendText(message + "\r\n");
                        }));
                    }
                    if ((bool)temp["success"]) {
                        if (txtExplorerPath != null) {
                            txtExplorerPath.Dispatcher.Invoke(new Action(() => {
                                foreach (dynamic key in temp["contentsList"].Children()) {
                                    if ((string)key["type"] == "file") {
                                        filesData.FilesAdd(new KLCFile((string)key["name"], (ulong)key["size"], (DateTime)key["date"]));
                                    } else if ((string)key["type"] == "folder") {
                                        viewFolders.Add(new KLCFile((string)key["name"], 0, (DateTime)key["date"]));
                                        listExplorerFolders.Items.Add((string)key["name"]);
                                    } else {
                                        Console.WriteLine("The hell?");
                                        //dgvExplorerFiles.Items.Add("??? - " + (string)key["name"]);
                                        // + " - Type: " + key["type"] + " - Size: " + key["size"] + " - Date: " + key["date"]
                                    }
                                }
                            }));
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

                case "GetFullDownloadItemList":
                    /* {
                         "name":[
                            "kworking"
                         ],
                         "type":"folder",
                         "size":-1,
                         "date":"2021-06-19T02:37:28.000Z"
                      }, */
                    if ((bool)temp["success"]) {
                        if (txtExplorerPath != null) {
                            txtExplorerPath.Dispatcher.Invoke(new Action(() => {
                                foreach (dynamic key in temp["contentsList"].Children()) {
                                    List<string> fileParts = key["name"].ToObject<List<string>>();
                                    string destination = downloadDestination + string.Join("\\", fileParts);

                                    if ((string)key["type"] == "file") {
                                        string selectedFile = fileParts.Last();
                                        fileParts.RemoveAt(fileParts.Count - 1);
                                        Download(selectedFile, destination, fileParts);
                                        //filesData.FilesAdd(new KLCFile((string)key["name"], (ulong)key["size"], (DateTime)key["date"]));
                                    } else if ((string)key["type"] == "folder") {
                                        if (!System.IO.Directory.Exists(destination))
                                            System.IO.Directory.CreateDirectory(destination);
                                    } else {
                                        Console.WriteLine("The hell?");
                                        //dgvExplorerFiles.Items.Add("??? - " + (string)key["name"]);
                                        // + " - Type: " + key["type"] + " - Size: " + key["size"] + " - Date: " + key["date"]
                                    }
                                }
                            }));
                        }
                    } else {
                        Console.WriteLine("The hell?");
                    }

                    break;

                default:
                    Console.WriteLine("FileExplorer message received: " + message);
                    break;
            }
            //}));
        }

        public void DeleteFolder(KLCFile klcFolder) {
            JObject jDelete = new JObject();
            jDelete["action"] = "DeleteItem";

            JArray jItems = new JArray();
            JObject jItemsObject = new JObject();
            jItemsObject["name"] = klcFolder.Name;
            jItemsObject["type"] = "folder";
            jItemsObject["size"] = -1;
            jItemsObject["date"] = klcFolder.Date;
            jItemsObject["rowSelected"] = true;
            jItems.Add(jItemsObject);
            jDelete["items"] = jItems;

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
            jDelete["path"] = jGetPath;
            jDelete["id"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            serverB.Send(jDelete.ToString());
        }

        public void DeleteFile(KLCFile klcFile) {
            JObject jDelete = new JObject();
            jDelete["action"] = "DeleteItem";

            JArray jItems = new JArray();
            JObject jItemsObject = new JObject();
            jItemsObject["name"] = klcFile.Name;
            jItemsObject["type"] = "file";
            jItemsObject["size"] = klcFile.Size;
            jItemsObject["date"] = klcFile.Date;
            jItemsObject["rowSelected"] = true;
            jItems.Add(jItemsObject);
            jDelete["items"] = jItems;

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
            jDelete["path"] = jGetPath;
            jDelete["id"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            serverB.Send(jDelete.ToString());
        }

        public void DownloadFolder(KLCFile klcFolder, string localPathToSaveIn) {
            downloadDestination = localPathToSaveIn;
            if (downloadDestination.Last() != '\\')
                downloadDestination += "\\";

            JObject jGetFull = new JObject();
            jGetFull["action"] = "GetFullDownloadItemList";

            JArray jItems = new JArray();
            JObject jItemsObject = new JObject();
            jItemsObject["name"] = klcFolder.Name;
            jItemsObject["type"] = "folder";
            jItemsObject["size"] = -1;
            jItemsObject["date"] = klcFolder.Date;
            jItemsObject["rowSelected"] = true;
            jItems.Add(jItemsObject);
            jGetFull["items"] = jItems;

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
            jGetFull["path"] = jGetPath;
            jGetFull["id"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            serverB.Send(jGetFull.ToString());
        }

        internal KLCFile GetKLCFolder(string valueName) {
            return viewFolders.FirstOrDefault(x => x.Name == valueName);
        }

        public void RenameFileOrFolder(string oldName, string newName) {
            //{ "action": "RenameItem", "sourcePath": [ "C:\\", "temp", "TestFolder4" ], "destinationPath": [ "C:\\", "temp", "TestFolder4a" ], "id": 1615086691458 }

            if (selectedPath.Count < 2)
                return;

            JObject jRename = new JObject();
            jRename["action"] = "RenameItem";
            JArray jSourcePath = new JArray();
            JArray jDestinationPath = new JArray();
            for (int i = 0; i < selectedPath.Count; i++) {
                if (i == 0) {
                    if (IsMac) {
                        jSourcePath.Add("/");
                        jDestinationPath.Add("/");
                    } else {
                        jSourcePath.Add(selectedPath[i] + "\\");
                        jDestinationPath.Add(selectedPath[i] + "\\");
                    }
                } else {
                    jSourcePath.Add(selectedPath[i]);
                    jDestinationPath.Add(selectedPath[i]);
                }
            }
            jSourcePath.Add(oldName);
            jDestinationPath.Add(newName);

            jRename["sourcePath"] = jSourcePath;
            jRename["destinationPath"] = jDestinationPath;
            jRename["id"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            serverB.Send(jRename.ToString());
        }

        public void CreateFolder(string newFolder) {
            if (selectedPath.Count == 0)
                return;

            //{ "action": "CreateFolder", "path": [ "C:\\", "temp", "TestFolder" ], "id": 1615078749826 }

            JObject jCreate = new JObject();
            jCreate["action"] = "CreateFolder";
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
            jGetPath.Add(newFolder);
            jCreate["path"] = jGetPath;
            jCreate["id"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            serverB.Send(jCreate.ToString());
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

                    progressBar.Dispatcher.Invoke(new Action(() => {
                        //txtBox.Text = "Downloading " + queuedDownload.fileName;
                        progressBar.Value = 0;
                        progressText.Text = "";
                    }));

                    JObject jDown = new JObject();

                    jDown["action"] = "Begin";
                    JArray jDownPath = new JArray();
                    for (int i = 0; i < activeDownload.Path.Count; i++) {
                        if (i == 0) {
                            if (IsMac)
                                jDownPath.Add("/");
                            else
                                jDownPath.Add(activeDownload.Path[i] + "\\");
                        } else
                            jDownPath.Add(activeDownload.Path[i]);
                    }
                    jDown["path"] = jDownPath;
                    jDown["filename"] = activeDownload.fileName;
                    jDown["type"] = "file";
                    jDown["fileID"] = activeDownload.fileID;

                    //--

                    string sendjson = jDown.ToString();
                    int jsonLen = sendjson.Length;
                    int totalLen = jsonLen;// + content.Length;
                    byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);
                    //byte[] contentBuffer = System.Text.Encoding.UTF8.GetBytes(content);

                    byte[] tosend = new byte[totalLen + 4];
                    byte[] tosendPrefix = BitConverter.GetBytes(jsonLen).Reverse().ToArray();
                    Array.Copy(tosendPrefix, 0, tosend, 0, tosendPrefix.Length);
                    Array.Copy(jsonBuffer, 0, tosend, 4, jsonLen);
                    //Array.Copy(contentBuffer, 0, tosend, 4 + jsonLen, content.Length);

                    activeDownload.Open();
                    if (serverBdownload != null)
                        serverBdownload.Send(tosend);
                    break;

                case "Data":
                    //{"action":"Data","fileSize":663,"requiresAck":true}[Data]
                    Console.WriteLine(jsonstr);
                    activeDownload.WriteData(remaining);

                    long total = (long)json["fileSize"];

                    int percentage = (int)((activeDownload.GetCurrentSize() / (double)total) * 100);

                    progressBar.Dispatcher.Invoke(new Action(() => {
                        progressBar.Value = percentage;
                        progressText.Text = "Download: " + activeDownload.fileName + " " + percentage + "%";
                    }));

                    if (json["requiresAck"] != null) {
                        if ((bool)json["requiresAck"] == true) {
                            #region Ack
                            JObject jAck = new JObject();
                            jAck["action"] = "DataAck";
                            jAck["filename"] = activeDownload.fileName;
                            string sendjsonAck = jAck.ToString();
                            int jsonLenAck = sendjsonAck.Length;
                            int totalLenAck = jsonLenAck;// + content.Length;
                            byte[] jsonBufferAck = System.Text.Encoding.UTF8.GetBytes(sendjsonAck);

                            byte[] tosendAck = new byte[totalLenAck + 4];
                            byte[] tosendPrefixAck = BitConverter.GetBytes(jsonLenAck).Reverse().ToArray();
                            Array.Copy(tosendPrefixAck, 0, tosendAck, 0, tosendPrefixAck.Length);
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
                    Console.WriteLine(jsonstr);
                    activeDownload.Close();
                    serverBdownload.Close();

                    progressBar.Dispatcher.Invoke(new Action(() => {
                        progressBar.Value = 0;
                        progressText.Text = activeDownload.fileName + " downloaded!";
                        //txtBox.AppendText("\r\nDownload complete.");

                        btnDownload.IsEnabled = true;
                        btnUpload.IsEnabled = true;
                    }));

                    activeDownload = null;
                    StartNextDownload();
                    break;

                case "Error":
                    Console.WriteLine("Download failed");
                    activeDownload.Close();
                    serverBdownload.Close();
                    break;

                default:
                    Console.WriteLine();
                    break;
            }
        }

        public void HandleUpload(byte[] data) {
            int jsonLength = BitConverter.ToInt32(data, 0).SwapEndianness();
            string jsonstr = Encoding.UTF8.GetString(data, 4, jsonLength);
            dynamic json = JsonConvert.DeserializeObject(jsonstr);

            int remStart = 4 + jsonLength;
            int remLength = data.Length - remStart;
            byte[] remaining = new byte[remLength];
            if (remLength > 0)
                Array.Copy(data, remStart, remaining, 0, remLength);

            switch (json["action"].ToString()) {
                case "UploadReady":
                    if (progressBar != null) {
                        progressBar.Dispatcher.Invoke(new Action(() => {
                            //txtBox.Text = "Uploading " + queuedUpload.fileName;
                            progressBar.Value = 0;
                            progressText.Text = "";
                        }));
                    }
                    //{"action":"UploadReady","filename":"000095 - Mission-Vision-Values Wallpapers_FA_v3_1920x1080.jpg"}
                    activeUpload.Open();
                    UploadBlock();
                    break;

                case "UploadStatus":
                    //{"action":"UploadStatus","filename":"BASETechConsole-7.00.21-20201218.exe","bytesWritten":131072,"fileID":1614998595945}
                    Console.WriteLine(jsonstr);
                    long written = (long)json["bytesWritten"];

                    if (progressBar != null) {
                        int percentage = (int)((written / (double)activeUpload.GetFileSize()) * 100);

                        progressBar.Dispatcher.Invoke(new Action(() => {
                            progressBar.Value = percentage;
                            progressText.Text = "Upload: " + activeUpload.fileName + " " + percentage + "%";
                        }));
                    }

                    if (written < activeUpload.GetFileSize())
                        UploadBlock();
                    break;

                case "UploadComplete":
                    Console.WriteLine(jsonstr);
                    activeUpload.Close();
                    serverBupload.Close();

                    if (progressBar != null) {
                        progressBar.Dispatcher.Invoke(new Action(() => {
                            progressBar.Value = 0;
                            progressText.Text = activeUpload.fileName + " uploaded!";
                            //txtBox.AppendText("\r\nUpload complete.");

                            btnDownload.IsEnabled = true;
                            btnUpload.IsEnabled = true;
                        }));
                    }

                    activeUpload = null;
                    StartNextUpload();
                    break;

                default:
                    Console.WriteLine(jsonstr);
                    break;
            }
        }

        private void UploadBlock() {
            JObject jUpload = new JObject();
            jUpload["action"] = "Data";
            jUpload["fileID"] = activeUpload.fileID;

            //--

            byte[] content = activeUpload.ReadBlock();

            string sendjson = jUpload.ToString();
            int jsonLen = sendjson.Length;
            int totalLen = jsonLen + content.Length;
            byte[] jsonBuffer = System.Text.Encoding.UTF8.GetBytes(sendjson);

            byte[] tosend = new byte[totalLen + 4];
            byte[] tosendPrefix = BitConverter.GetBytes(jsonLen).Reverse().ToArray();
            Array.Copy(tosendPrefix, 0, tosend, 0, tosendPrefix.Length);
            Array.Copy(jsonBuffer, 0, tosend, 4, jsonLen);
            Array.Copy(content, 0, tosend, 4 + jsonLen, content.Length);

            if (serverB != null)
                serverBupload.Send(tosend);
        }

        public void Download(string selectedFile, string saveFile, List<string> appendPath = null) {
            List<string> path = new List<string>(selectedPath);
            if (appendPath != null) {
                //Download folder
                foreach (string part in appendPath)
                    path.Add(part);
            }

            queueDownload.Enqueue(new Download(path, selectedFile, saveFile, "file"));
            if (activeDownload == null)
                StartNextDownload();
        }

        private void StartNextDownload() {
            if (activeDownload != null || queueDownload.Count == 0)
                return;
            activeDownload = queueDownload.Dequeue();
            activeDownload.GenFileId();

            JObject jDown = new JObject();

            jDown["action"] = "Download";
            JArray jDownPath = new JArray();
            for (int i = 0; i < activeDownload.Path.Count; i++) {
                if (i == 0) {
                    if (IsMac)
                        jDownPath.Add("/");
                    else
                        jDownPath.Add(activeDownload.Path[i] + "\\");
                } else
                    jDownPath.Add(activeDownload.Path[i]);
            }
            jDown["path"] = jDownPath;
            jDown["filename"] = activeDownload.fileName;
            jDown["type"] = "file";
            jDown["fileId"] = activeDownload.fileID;
            jDown["id"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            serverB.Send(jDown.ToString());

            //txtBox.Text = "Starting download: " + saveFile;
        }

        public bool Upload(string openFile) {
            List<string> usePath = new List<string>(selectedPath);

            if (selectedPath.Count == 0) {
                if (progressBar == null) {
                    if(IsMac) {
                        return false;
                    } else {
                        usePath.Add("C:");
                        usePath.Add("temp");
                    }
                } else
                    return false;
            };

            string selectedFile = System.IO.Path.GetFileName(openFile);
            long fileID = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            queueUpload.Enqueue(new Upload(usePath, selectedFile, openFile, fileID, "file"));

            if (activeUpload == null)
                StartNextUpload();

            return true;
        }

        private void StartNextUpload() {
            if (queueUpload.Count == 0)
                return;
            activeUpload = queueUpload.Dequeue();

            JObject jUpload = new JObject();

            jUpload["action"] = "Upload";
            jUpload["fileID"] = activeUpload.fileID;
            JArray jUploadPath = new JArray();
            for (int i = 0; i < activeUpload.Path.Count; i++) {
                if (i == 0) {
                    if (IsMac)
                        jUploadPath.Add("/");
                    else
                        jUploadPath.Add(activeUpload.Path[i] + "\\");
                } else
                    jUploadPath.Add(activeUpload.Path[i]);
            }
            jUpload["sourcePath"] = jUploadPath;
            jUpload["file"] = activeUpload.fileName;
            jUpload["size"] = activeUpload.GetFileSize();
            jUpload["type"] = "file";

            JObject jPerm = new JObject();
            JObject jPermExec = new JObject();
            jPermExec["owner"] = 0;
            jPermExec["group"] = 0;
            jPermExec["others"] = 0;
            jPerm["isReadOnly"] = false;
            jPerm["execPerms"] = jPermExec;
            jUpload["permissions"] = jPerm;

            jUpload["date"] = DateTimeOffset.UtcNow.ToString("s") + "Z";
            jUpload["id"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            serverB.Send(jUpload.ToString());
        }

        private void Update() {
            if (serverB == null)
                return;

            //{"action":"GetFolderContents","path":["C:\\","temp"],"id":1582283274052}
            JObject jGet = new JObject();

            if (txtExplorerPath != null) {
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
