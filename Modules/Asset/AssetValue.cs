using System;

namespace KLC_Finch.Modules {
    public class AssetValue : IComparable {

        public string Key { get; private set; }
        public string Value { get; private set; }

        public AssetValue(dynamic a) {
            Key = "";
            Value = "";
        }

        public int CompareTo(object obj) {
            return Key.CompareTo(((AssetValue)obj).Key);
        }

        public override string ToString() {
            return Key;
        }

    }
}
