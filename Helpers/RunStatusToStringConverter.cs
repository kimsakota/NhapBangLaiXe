using System;
using System.Globalization;
using System.Windows.Data;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls; // Cần thiết cho ControlAppearance

namespace ToolVip.Helpers
{
    // 1. Converter đổi chữ nút (Bắt đầu / Dừng lại)
    public class RunStatusToStringConverter : IValueConverter
    {
        // Singleton instance để dùng trực tiếp trong XAML: Converter={x:Static local:RunStatusToStringConverter.Instance}
        public static readonly RunStatusToStringConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Nếu đang chạy (true) -> Hiện chữ "DỪNG LẠI"
            // Nếu đang dừng (false) -> Hiện chữ "BẮT ĐẦU AUTO"
            if (value is bool isRunning && isRunning)
            {
                return "DỪNG LẠI";
            }
            return "BẮT ĐẦU AUTO";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    // 2. Converter đổi màu nút (Xanh / Đỏ)
    public class RunStatusToAppearanceConverter : IValueConverter
    {
        public static readonly RunStatusToAppearanceConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Nếu đang chạy -> Màu Đỏ (Danger) để cảnh báo nút Dừng
            if (value is bool isRunning && isRunning)
            {
                return ControlAppearance.Danger;
            }
            // Nếu đang dừng -> Màu Xanh (Primary) để mời gọi bấm
            return ControlAppearance.Primary;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}