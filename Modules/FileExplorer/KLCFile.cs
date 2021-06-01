using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLC_Finch {
    public class KLCFile : IComparable {
        public string Name { get; private set; }
        public ulong Size { get; private set; }
        public string Ext { get; private set; }
        public DateTime Date { get; private set; }

        public KLCFile(string Name, ulong Size, DateTime Date) {
            this.Name = Name;
            this.Size = Size;
            this.Date = Date.ToLocalTime();

            string[] parts = Name.Split('.');
            if (parts.Length > 1)
                this.Ext = parts.Last();
            else
                this.Ext = "";
        }

        public int CompareTo(object obj) {
            return Name.CompareTo(((KLCFile)obj).Name);
        }

    }
}
