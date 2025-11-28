using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;

namespace ToolVip.Helpers
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        // Singleton instance để dùng trực tiếp trong XAML
        public static readonly BooleanToVisibilityConverter Instance = new();

        public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool flag)
            {
                if (parameter?.ToString() == "Reverse")
                    flag = !flag;

                return flag ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object? ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility vis)
            {
                bool result = vis == Visibility.Visible;

                if (parameter?.ToString() == "Reverse")
                    result = !result;

                return result;
            }

            return false;
        }
    }
}
