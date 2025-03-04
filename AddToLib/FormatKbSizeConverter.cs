﻿using System;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Data;

namespace KLC_Finch {
    public class FormatKbSizeConverter : IValueConverter {
        //https://thomasfreudenberg.com/archive/2017/01/21/presenting-byte-size-values-in-wpf/

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
        public static extern long StrFormatByteSizeW(long qdw, [MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszBuf,
            int cchBuf);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var number = System.Convert.ToInt64(value);
            var sb = new StringBuilder(32);
            StrFormatByteSizeW(number, sb, sb.Capacity);
            return sb.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return DependencyProperty.UnsetValue;
        }
    }
}
