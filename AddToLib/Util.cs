using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace KLC {
    public static class Util
    {

        public static string DecodeBase64(string base64)
        {
            if (base64.Length % 4 > 0)
                base64 = base64.PadRight(base64.Length + 4 - base64.Length % 4, '=');
            return Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }

        public static string EncodeToBase64(string input) {
            byte[] plainTextBytes = Encoding.UTF8.GetBytes(input);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static System.Windows.Forms.KeyEventArgs ToWinforms(this System.Windows.Input.KeyEventArgs keyEventArgs) {
            // So far this ternary remained pointless, might be useful in some very specific cases though
            var wpfKey = keyEventArgs.Key == System.Windows.Input.Key.System ? keyEventArgs.SystemKey : keyEventArgs.Key;
            var winformModifiers = keyEventArgs.KeyboardDevice.Modifiers.ToWinforms();
            var winformKeys = (System.Windows.Forms.Keys)System.Windows.Input.KeyInterop.VirtualKeyFromKey(wpfKey);
            return new System.Windows.Forms.KeyEventArgs(winformKeys | winformModifiers);
        }

        public static System.Windows.Forms.Keys ToWinforms(this System.Windows.Input.ModifierKeys modifier) {
            var retVal = System.Windows.Forms.Keys.None;
            if (modifier.HasFlag(System.Windows.Input.ModifierKeys.Alt)) {
                retVal |= System.Windows.Forms.Keys.Alt;
            }
            if (modifier.HasFlag(System.Windows.Input.ModifierKeys.Control)) {
                retVal |= System.Windows.Forms.Keys.Control;
            }
            if (modifier.HasFlag(System.Windows.Input.ModifierKeys.None)) {
                // Pointless I know
                retVal |= System.Windows.Forms.Keys.None;
            }
            if (modifier.HasFlag(System.Windows.Input.ModifierKeys.Shift)) {
                retVal |= System.Windows.Forms.Keys.Shift;
            }
            if (modifier.HasFlag(System.Windows.Input.ModifierKeys.Windows)) {
                // Not supported lel
            }
            return retVal;
        }

        public static string FuzzyTimeAgo(DateTime dt) {
            TimeSpan span = DateTime.Now - dt;
            if (span.Days > 365) {
                int years = (span.Days / 365);
                if (span.Days % 365 != 0)
                    years += 1;
                return String.Format("{0} {1} ago",
                years, years == 1 ? "year" : "years");
            }
            if (span.Days > 30) {
                int months = (span.Days / 30);
                if (span.Days % 31 != 0)
                    months += 1;
                return String.Format("{0} {1} ago",
                months, months == 1 ? "month" : "months");
            }
            if (span.Days > 0)
                return String.Format("{0} {1} ago",
                span.Days, span.Days == 1 ? "day" : "days");
            if (span.Hours > 0)
                return String.Format("{0} {1} ago",
                span.Hours, span.Hours == 1 ? "hour" : "hours");
            if (span.Minutes > 0)
                return String.Format("{0} {1} ago",
                span.Minutes, span.Minutes == 1 ? "minute" : "minutes");
            //if (span.Seconds > 5)
            return String.Format("{0} seconds ago", span.Seconds);
            //if (span.Seconds <= 5)
            //    return "just now";
            //return string.Empty;
        }

        public static string JsonPrettify(string json) {
            using (var stringReader = new StringReader(json))
            using (var stringWriter = new StringWriter()) {
                var jsonReader = new JsonTextReader(stringReader);
                var jsonWriter = new JsonTextWriter(stringWriter) { Formatting = Formatting.Indented };
                jsonWriter.WriteToken(jsonReader);
                return stringWriter.ToString();
            }
        }

    }
}
