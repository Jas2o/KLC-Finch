using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KLC_Finch {
    public class KLCFile {
        public string Name;
        public long Size;
        //public string Type;
        public DateTime Date;

        public KLCFile(string Name, long Size, DateTime Date) {
            this.Name = Name;
            this.Size = Size;
            this.Date = Date;
        }

    }
}
