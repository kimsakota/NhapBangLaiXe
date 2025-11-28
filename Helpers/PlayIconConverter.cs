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
    public class PlayIconConverter : IValueConverter
    {
        public static readonly PlayIconConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isPlaying = (bool)value;
            return isPlaying ? SymbolRegular.RecordStop24 : SymbolRegular.Play24;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return System.Windows.Data.Binding.DoNothing;
        }
    }
}
