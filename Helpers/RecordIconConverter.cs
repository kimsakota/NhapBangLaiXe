using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using Wpf.Ui.Controls;

namespace ToolVip.Helpers
{
    public class RecordIconConverter : IValueConverter
    {
        public static readonly RecordIconConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isRecording = (bool)value;
            return isRecording ? SymbolRegular.RecordStop24 : SymbolRegular.Record24;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
