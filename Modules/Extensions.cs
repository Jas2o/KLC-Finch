using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace KLC_Finch {
    public static class Extensions {

        public static List<int> AllIndexesOf(this string str, string value) {
            //https://stackoverflow.com/questions/2641326/finding-all-positions-of-substring-in-a-larger-string-in-c-sharp

            if (String.IsNullOrEmpty(value))
                throw new ArgumentException("the string to find may not be empty", "value");
            List<int> indexes = new List<int>();
            for (int index = 0; ; index += value.Length) {
                index = str.IndexOf(value, index);
                if (index == -1)
                    return indexes;
                indexes.Add(index);
            }
        }

        public static int SwapEndianness(this int value) {
            var b1 = (value >> 0) & 0xff;
            var b2 = (value >> 8) & 0xff;
            var b3 = (value >> 16) & 0xff;
            var b4 = (value >> 24) & 0xff;

            return b1 << 24 | b2 << 16 | b3 << 8 | b4 << 0;
        }

        /*
        public static void AppendText(this RichTextBox box, string text, Color foreColor, Color backColor) {
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;

            box.SelectionColor = (foreColor != null ? foreColor : box.ForeColor);
            box.SelectionBackColor = (backColor != null ? backColor : box.BackColor);
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
            box.SelectionColor = box.BackColor;
        }

        public static void AppendText(this RichTextBox box, string text, Color foreColor, Color backColor, int line, int pos) {
            box.SelectionStart = box.GetFirstCharIndexFromLine(line) + pos;
            box.SelectionLength = 0;

            box.SelectionColor = (foreColor != null ? foreColor : box.ForeColor);
            box.SelectionBackColor = (backColor != null ? backColor : box.BackColor);
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
            box.SelectionColor = box.BackColor;
        }
        */

        public static void AppendText(this RichTextBox box, string text, System.Windows.Media.Color foreColor) {
            TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);
            tr.Text = text;
            try {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(foreColor));
            } catch (FormatException) { }

            box.ScrollToEnd();
        }

        public static void AppendText(this RichTextBox box, string text, System.Windows.Media.Color foreColor, System.Windows.Media.Color backColor) {
            TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);
            tr.Text = text;
            try {
                tr.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(backColor));
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(foreColor));
            } catch (FormatException) { }

            box.ScrollToEnd();
        }

        public static void AppendText(this RichTextBox box, string text, Color foreColor, Color backColor, int line, int pos) {
            /*
            box.SelectionStart = box.GetFirstCharIndexFromLine(line) + pos;
            box.SelectionLength = 0;

            box.SelectionColor = (foreColor != null ? foreColor : box.ForeColor);
            box.SelectionBackColor = (backColor != null ? backColor : box.BackColor);
            box.AppendText(text);
            box.SelectionColor = box.ForeColor;
            box.SelectionColor = box.BackColor;
            */

            TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);
            tr.Text = text;
            try {
                tr.ApplyPropertyValue(TextElement.BackgroundProperty, new SolidColorBrush(backColor));
                tr.ApplyPropertyValue(TextElement.ForegroundProperty, new SolidColorBrush(foreColor));
            } catch (FormatException) { }

            box.ScrollToEnd();
        }

        public static string Truncate(this string value, int maxChars) {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars) + "...";
        }

        public static string TruncateEnd(this string value, int maxChars) {
            return value.Length <= maxChars ? value : "..." + value.Substring(value.Length-maxChars, maxChars);
        }

    }
}
