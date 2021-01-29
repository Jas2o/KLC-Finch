using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLC_Finch {
    public class RegistryValue {
        public string Name;
        public string Type;
        public dynamic Data;

        public RegistryValue(string Name) {
            this.Name = Name;
            this.Type = "REG_NONE";
            this.Data = "";
        }

        public RegistryValue(string Name, string Type, dynamic Data) {
            this.Name = Name;
            this.Type = Type;

            if (Type == "REG_BINARY") {
                this.Data = ((JArray)Data).Select(jv => (byte)jv).ToArray();
            } else if (Type == "REG_MULTI_SZ") {
                this.Data = ((JArray)Data).Select(jv => (string)jv).ToArray();
            } else {
                this.Data = Data;
            }
        }

        public RegistryValue(string Name, string Data, bool isExpand=false) {
            this.Name = Name;
            this.Type = (isExpand ? "REG_EXPAND_SZ" : "REG_SZ");
            this.Data = Data;
        }

        public RegistryValue(string Name, string[] Data) {
            this.Name = Name;
            this.Type = "REG_MULTI_SZ";
            this.Data = Data;
        }

        public RegistryValue(string Name, int Data) {
            this.Name = Name;
            this.Type = "REG_DWORD";
            this.Data = Data;
        }

        public RegistryValue(string Name, long Data) {
            this.Name = Name;
            this.Type = "REG_QWORD";
            this.Data = Data;
        }

        public RegistryValue(string Name, byte[] Data) {
            this.Name = Name;
            this.Type = "REG_BINARY";
            this.Data = Data;
        }

        public override string ToString() {
            switch (Type) {
                case "REG_NONE":
                    return "";
                case "REG_SZ":
                case "REG_EXPAND_SZ":
                    return Data.ToString();
                case "REG_MULTI_SZ":
                    return string.Join(" ", Data);
                case "REG_DWORD":
                case "REG_QWORD":
                    return string.Format("0x{0} ({1})", Data.ToString("X").ToLower(), Data.ToString());
                case "REG_BINARY":
                    return BitConverter.ToString(Data).Replace('-', ' ').ToLower();
            }
            return base.ToString();
        }
    }
}
