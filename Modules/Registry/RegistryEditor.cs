using Fleck;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web;
using System.Windows.Controls;

namespace KLC_Finch {
    public class RegistryEditor {

        private static string modulename = "registryeditor";
        public static Dictionary<int, string> LabelForKey = new Dictionary<int, string>() {
            {0, "HKEY_CLASSES_ROOT" },
            {1, "HKEY_CURRENT_USER" },
            {2, "HKEY_LOCAL_MACHINE" },
            {3, "HKEY_USERS" },
            {7, "HKEY_CURRENT_CONFIG" },
        };
        public static Dictionary<string, int> IdForKey = new Dictionary<string, int>() {
            {"HKEY_CLASSES_ROOT", 0}, {"HKCR", 0},
            {"HKEY_CURRENT_USER", 1}, {"HKCU", 1},
            {"HKEY_LOCAL_MACHINE", 2}, {"HKLM", 2},
            {"HKEY_USERS", 3}, {"HKU", 3},
            {"HKEY_CURRENT_CONFIG", 7}, {"HKCC", 7},
        };

        private IWebSocketConnection serverB;
        private ListView lvRegistryKeys;
        private DataGrid dgvRegistryValues;
        private TextBox txtRegistryPath;
        private TextBox txtBox;

        private int selectedHive;
        private List<string> selectedPath;
        private List<string> viewKeys;
        private List<RegistryValue> viewValues;

        public RegistryEditor(KLC.LiveConnectSession session, ListView lvRegistryKeys, DataGrid dgvRegistryValues, TextBox txtRegistryPath, TextBox txtBox = null) {
            this.lvRegistryKeys = lvRegistryKeys;
            this.dgvRegistryValues = dgvRegistryValues;
            this.txtRegistryPath = txtRegistryPath;
            this.txtBox = txtBox;

            selectedHive = -1;
            selectedPath = new List<string>();
            viewKeys = new List<string>();
            viewValues = new List<RegistryValue>();

            if(session != null)
                session.WebsocketB.ControlAgentSendTask(modulename);
        }

        public void SetSocket(IWebSocketConnection ServerBsocket) {
            this.serverB = ServerBsocket;
        }

        public void Receive(string message) {
            txtBox.Dispatcher.Invoke(new Action(() => {
                dynamic temp = JsonConvert.DeserializeObject(message);
                switch ((string)temp["action"]) {
                    case "ScriptReady":
                        Update();
                        break;
                    case "GetHives":
                        viewKeys.Clear();
                        if ((bool)temp["success"]) {
                            foreach (dynamic hive in temp["hives"].Children()) {
                                if ((string)hive["type"] == "key")
                                    viewKeys.Add(LabelForKey.ContainsKey((int)hive["name"]) ? LabelForKey[(int)hive["name"]] : (string)hive["name"]);
                                else
                                    throw new NotImplementedException();
                            }
                        }
                        UpdateDisplay(false);
                        break;
                    case "GetSubKeys":
                    case "CreateKey":
                        viewKeys.Clear();
                        if ((bool)temp["success"]) {
                            foreach (dynamic key in temp["keys"].Children())
                                viewKeys.Add(key["name"].ToString());
                        }
                        UpdateDisplayKeys(true);
                        break;
                    case "GetKeyValues":
                    case "CreateValue":
                    case "ModifyValue":
                    case "RenameItem":
                    case "DeleteItem":
                        if ((bool)temp["success"]) {
                            if (temp["values"] != null) {
                                viewValues.Clear();
                                foreach (dynamic value in temp["values"].Children()) {
                                    RegistryValue rv;
                                    if (value["valueType"] == null || value["data"] == null)
                                        rv = new RegistryValue(value["name"].ToString());
                                    else
                                        rv = new RegistryValue(value["name"].ToString(), value["valueType"].ToString(), value["data"]);

                                    if (viewValues.FirstOrDefault(x => x.Name == rv.Name) == null) {
                                        //This fixes a stupid Kaseya bug involving the (Default) value item appearing twice.
                                        viewValues.Add(rv);
                                    }
                                }
                                UpdateDisplayValues();
                            } else if (temp["keys"] != null) {
                                //Triggered when renaming a key, similar to GetSubKeys but it doesn't refresh properly.
                                viewKeys.Clear();
                                if ((bool)temp["success"]) {
                                    foreach (dynamic key in temp["keys"].Children())
                                        viewKeys.Add(key["name"].ToString());
                                }
                                Update(); //This allows us to stay in the renamed key rather than be kicked out and not get the values there.
                            }
                        }

                        break;
                    default:
                        txtBox.AppendText("RegistryEditor message received: " + message + "\r\n");
                        break;
                }
            }));
        }

        /*
        private WebSocket WS_EdgeRelay(string authPayloadjsonWebToken, string sessionId) {

            string pathModule = Util.EncodeToBase64("/app/" + modulename);

            WebSocket websocket = new WebSocket("wss://vsa-web.company.com.au:443/kaseya/edge/relay?auth=" + authPayloadjsonWebToken + "&relayId=" + sessionId + "|" + pathModule);

            websocket.AutoSendPingInterval = 5;
            websocket.EnableAutoSendPing = true;
            if (txtBox != null) {
                websocket.Opened += (sender, e) => txtBox.Invoke(new Action(() => {
                    txtBox.AppendText("RegistryEditor Socket opened: " + sessionId + "\r\n");
                }));
                websocket.Closed += (sender, e) => txtBox.Invoke(new Action(() => {
                    txtBox.AppendText("RegistryEditor Socket closed: " + sessionId + " - " + e.ToString() + "\r\n");
                }));
                websocket.MessageReceived += (sender, e) => );
                websocket.Error += (sender, e) => txtBox.Invoke(new Action(() => {
                    txtBox.AppendText("RegistryEditor Socket error: " + sessionId + " - " + e.Exception.ToString() + "\r\n");
                }));
            } else {
                websocket.Opened += (sender, e) => Console.WriteLine("RegistryEditor Socket opened: " + sessionId);
                websocket.Closed += (sender, e) => Console.WriteLine("RegistryEditor Socket closed: " + sessionId + " - " + e.ToString());
                websocket.MessageReceived += (sender, e) => Console.WriteLine("RegistryEditor message received: " + sessionId + " - " + e.Message);
                websocket.Error += (sender, e) => Console.WriteLine("RegistryEditor Socket error: " + sessionId + " - " + e.Exception.ToString());
            }

            Util.ConfigureProxy(websocket);

            websocket.Open();
            return websocket;
        }
        */

        /// <summary>
        /// Uses the privately set selected hive and key path to request registry data from the agent, which will subsequently update the UI with the results.
        /// </summary>
        private void Update() {
            JObject jGet = new JObject();

            if (selectedHive == -1) {
                txtRegistryPath.Text = "";
                jGet["action"] = "GetHives";
                serverB.Send(jGet.ToString());
            } else if(selectedPath.Count == 0) {
                txtRegistryPath.Text = LabelForKey[selectedHive];

                jGet["action"] = "GetSubKeys";
                jGet["hive"] = selectedHive;
                jGet["path"] = new JArray();
                serverB.Send(jGet.ToString());
                jGet["action"] = "GetKeyValues";
                serverB.Send(jGet.ToString());
            } else {
                txtRegistryPath.Text = LabelForKey[selectedHive] + "\\" + string.Join("\\", selectedPath);

                jGet["action"] = "GetSubKeys";
                jGet["hive"] = selectedHive;
                JArray jGetPath = new JArray();
                foreach (string item in selectedPath)
                    jGetPath.Add(item);
                jGet["path"] = jGetPath;
                serverB.Send(jGet.ToString());
                jGet["action"] = "GetKeyValues";
                serverB.Send(jGet.ToString());
            }
        }

        private void UpdateDisplay(bool sortKeys) {
            UpdateDisplayKeys(sortKeys);
            UpdateDisplayValues();
        }

        private void UpdateDisplayKeys(bool sortKeys) {
            lvRegistryKeys.Items.Clear();

            foreach (string key in viewKeys)
                lvRegistryKeys.Items.Add(key);

            /*
            lvRegistryKeys.Sorting = (sortKeys ? SortOrder.Ascending : SortOrder.None);
            lvRegistryKeys.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
            */
        }

        private void UpdateDisplayValues() {
            dgvRegistryValues.DataContext = null;

            DataTable dt = new DataTable();
            dt.Columns.Add("Name", typeof(string));
            dt.Columns.Add("Type", typeof(string));
            dt.Columns.Add("Data", typeof(string));

            if (selectedHive != -1 && viewValues.FirstOrDefault(x => x.Name == "") == null) {
                DataRow row = dt.NewRow();
                row[0] = "";
                row[1] = "REG_SZ";
                row[2] = "(value not set)";
                dt.Rows.Add(row);
            }

            foreach (RegistryValue value in viewValues) {
                DataRow row = dt.NewRow();
                row[0] = value.Name; //Can't really change to (Default) cause of Kaseya issues
                row[1] = value.Type;
                row[2] = value.ToString();
                dt.Rows.Add(row);
            }

            dgvRegistryValues.DataContext = dt;
            //dgvRegistryValues.AutoResizeColumns();
            //dgvRegistryValues.Sort(dgvRegistryValues.Columns[0], System.ComponentModel.ListSortDirection.Ascending);
        }

        public RegistryValue GetRegistryValue(string valueName) {
            return viewValues.FirstOrDefault(x => x.Name == valueName);
        }

        /// <summary>
        /// Jump into a given hive or key.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="isHive"></param>
        public void SelectKey(string key, bool isHive=false) {
            if (isHive)
                selectedHive = IdForKey[key];
            else
                selectedPath.Add(key);

            Update();
        }

        /// <summary>
        /// Jump the registry browser to a given registry path. Supports skipping Computer\ at the start and translating common HK abbreviations.
        /// </summary>
        /// <param name="input">String separated by backslashes.</param>
        public void GoTo(string input) {
            string[] path = input.Split('\\');

            selectedHive = -1;
            foreach(string p in path) {
                if(selectedHive != -1) {
                    selectedPath.Add(p);
                } else {
                    if(IdForKey.ContainsKey(p.ToUpper())) {
                        selectedHive = IdForKey[p.ToUpper()];
                        selectedPath.Clear();
                    }
                }
            }

            Update();
        }

        /// <summary>
        /// Jump the registry browser up a key or hive level.
        /// </summary>
        public void GoUp() {
            if (selectedPath.Count == 0)
                selectedHive = -1;
            else
                selectedPath.RemoveAt(selectedPath.Count - 1);
            
            Update();
        }

        public void CreateKey(string keyName) {
            if (selectedHive == -1)
                return;

            //{"action":"CreateKey","name":"aTest","hive":2,"path":["SOFTWARE"]}

            JObject jCreate = new JObject();
            jCreate["action"] = "CreateKey";
            jCreate["name"] = keyName;

            jCreate["hive"] = selectedHive;
            JArray jGetPath = new JArray();
            foreach (string item in selectedPath)
                jGetPath.Add(item);
            jCreate["path"] = jGetPath;

            serverB.Send(jCreate.ToString());
        }

        public void CreateValue(RegistryValue value) {
            if (selectedHive == -1)
                return;

            //{"action":"CreateValue","key":"SOFTWARE","name":"testString","type":"REG_SZ","value":"this is a \"string\"","hive":2,"path":[]}
            //{"action":"CreateValue","key":"SOFTWARE","name":"testDword100","type":"REG_DWORD","value":256,"hive":2,"path":[]}
            //{"action":"CreateValue","key":"SOFTWARE","name":"testQword100","type":"REG_QWORD","value":256,"hive":2,"path":[]}
            //{"action":"CreateValue","key":"SOFTWARE","name":"testMultiString","type":"REG_MULTI_SZ","value":["line1","line2","line3"],"hive":2,"path":[]}
            //{"action":"CreateValue","key":"SOFTWARE","name":"testExpandable","type":"REG_EXPAND_SZ","value":"this is an expandable string","hive":2,"path":[]}
            //{"action":"CreateValue","key":"SOFTWARE","name":"testBinary","type":"REG_BINARY","value":[222,173,190,239],"hive":2,"path":[]}

            //{"action":"CreateValue","key":"7-Zip","name":"testA","type":"REG_SZ","value":"testA","hive":2,"path":["SOFTWARE"]}

            JObject jCreate = new JObject();
            jCreate["action"] = "CreateValue";

            jCreate["name"] = value.Name;
            jCreate["type"] = value.Type;
            if (value.Type == "REG_MULTI_SZ")
                jCreate["value"] = new JArray(((string[])value.Data).Where(x => !string.IsNullOrEmpty(x)).ToArray());
            else if (value.Type == "REG_BINARY") {
                JArray ja = new JArray();
                foreach (byte b in value.Data)
                    ja.Add(b);
                jCreate["value"] = ja;
            } else
                jCreate["value"] = value.Data;

            jCreate["hive"] = selectedHive;
            jCreate["key"] = selectedPath[selectedPath.Count - 1];
            JArray jGetPath = new JArray();
            for (int i = 0; i < selectedPath.Count - 1; i++)
                jGetPath.Add(selectedPath[i]);
            jCreate["path"] = jGetPath;

            serverB.Send(jCreate.ToString());
        }

        public string GetKey() {
            if (selectedPath.Count == 0)
                return null;

            return selectedPath[selectedPath.Count - 1];
        }

        public void ModifyValue(RegistryValue value) {
            if (selectedHive == -1)
                return;

            //{"action":"ModifyValue","key":"aTestA","name":"testRename","type":"REG_SZ","value":"testModify","hive":2,"path":["SOFTWARE"]}

            JObject jModify = new JObject();
            jModify["action"] = "ModifyValue";

            jModify["name"] = value.Name;
            jModify["type"] = value.Type;
            if (value.Type == "REG_MULTI_SZ")
                jModify["value"] = new JArray(((string[])value.Data).Where(x => !string.IsNullOrEmpty(x)).ToArray());
            else if (value.Type == "REG_BINARY") {
                JArray ja = new JArray();
                foreach(byte b in value.Data)
                    ja.Add(b);
                jModify["value"] = ja;
            } else
                jModify["value"] = value.Data;

            jModify["hive"] = selectedHive;
            jModify["key"] = selectedPath[selectedPath.Count - 1];
            JArray jGetPath = new JArray();
            for (int i = 0; i < selectedPath.Count - 1; i++)
                jGetPath.Add(selectedPath[i]);
            jModify["path"] = jGetPath;

            serverB.Send(jModify.ToString());
        }

        public void RenameKey(string keyOld, string keyNew) {
            if (selectedHive == -1 || selectedPath.Count == 0)
                return;

            //{"action":"RenameItem","hive":2,"path":["SOFTWARE"],"key":"aTest","newName":"aTestA"}

            JObject jRename = new JObject();
            jRename["action"] = "RenameItem";

            jRename["hive"] = selectedHive;
            jRename["key"] = keyOld;
            jRename["newName"] = keyNew;
            JArray jGetPath = new JArray();
            for (int i = 0; i < selectedPath.Count - 1; i++)
                jGetPath.Add(selectedPath[i]);
            jRename["path"] = jGetPath;

            selectedPath[selectedPath.Count - 1] = keyNew;
            txtRegistryPath.Text = LabelForKey[selectedHive] + "\\" + string.Join("\\", selectedPath);

            serverB.Send(jRename.ToString());
        }

        public void RenameValue(string nameOld, RegistryValue value) {
            if (selectedHive == -1)
                return;

            //{"action":"RenameItem","hive":2,"path":["SOFTWARE"],"key":"aTestA","value":"TestStringA","newName":"TestStringB","type":"REG_SZ","data":"exampleTest"}
            // For some reason "value" is the old name, and "data" is the value... Fucking Kaseya

            JObject jRename = new JObject();
            jRename["action"] = "RenameItem";

            jRename["value"] = nameOld;
            jRename["newName"] = value.Name;
            jRename["type"] = value.Type;
            if (value.Type == "REG_MULTI_SZ")
                jRename["data"] = new JArray(((string[])value.Data).Where(x => !string.IsNullOrEmpty(x)).ToArray());
            else if (value.Type == "REG_BINARY") {
                JArray ja = new JArray();
                foreach (byte b in value.Data)
                    ja.Add(b);
                jRename["data"] = ja;
            } else
                jRename["data"] = value.Data;

            jRename["hive"] = selectedHive;
            jRename["key"] = selectedPath[selectedPath.Count - 1];
            JArray jGetPath = new JArray();
            for (int i = 0; i < selectedPath.Count - 1; i++)
                jGetPath.Add(selectedPath[i]);
            jRename["path"] = jGetPath;

            serverB.Send(jRename.ToString());
        }

        public void DeleteValue(string valueName) {
            if (selectedHive == -1)
                return;

            //{"action":"DeleteItem","hive":2,"path":[],"key":"SOFTWARE","value":"testBinary"}
            

            JObject jDelete = new JObject();
            jDelete["action"] = "DeleteItem";
            jDelete["value"] = valueName;

            jDelete["hive"] = selectedHive;
            jDelete["key"] = selectedPath[selectedPath.Count - 1];
            JArray jGetPath = new JArray();
            for (int i = 0; i < selectedPath.Count - 1; i++)
                jGetPath.Add(selectedPath[i]);
            jDelete["path"] = jGetPath;

            serverB.Send(jDelete.ToString());
        }

        public void DeleteKey(string keyName) {
            if (selectedHive == -1 || selectedPath.Count == 0)
                return;

            selectedPath.RemoveAt(selectedPath.Count - 1);
            txtRegistryPath.Text = LabelForKey[selectedHive] + "\\" + string.Join("\\", selectedPath);

            //{"action":"DeleteItem","hive":2,"path":["SOFTWARE","aTestJ"],"key":"TestSub2"}

            JObject jDelete = new JObject();
            jDelete["action"] = "DeleteItem";

            jDelete["hive"] = selectedHive;
            jDelete["key"] = keyName; //What we're deleting
            JArray jGetPath = new JArray();
            for (int i = 0; i < selectedPath.Count; i++) //Different to the other copy/paste
                jGetPath.Add(selectedPath[i]);
            jDelete["path"] = jGetPath;

            serverB.Send(jDelete.ToString());
        }

    }

}
