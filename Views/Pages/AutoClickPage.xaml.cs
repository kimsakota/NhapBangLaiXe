using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;
using ToolVip.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Appearance; // Dùng cho ControlAppearance
using Wpf.Ui.Controls;   // Dùng cho INavigableView

namespace ToolVip.Views.Pages
{
    public partial class AutoClickPage : Page, INavigableView<AutoClickViewModel>
    {
        public AutoClickViewModel ViewModel { get; }

        public AutoClickPage(AutoClickViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }

    // Converter Text Button
    public class RunStatusToStringConverter : IValueConverter
    {
        public static readonly RunStatusToStringConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "DỪNG LẠI" : "BẮT ĐẦU AUTO";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    // Converter Màu Button
    public class RunStatusToAppearanceConverter : IValueConverter
    {
        public static readonly RunStatusToAppearanceConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Trả về enum ControlAppearance.Danger (Đỏ) hoặc Primary (Xanh/Mặc định)
            return (bool)value ? ControlAppearance.Danger : ControlAppearance.Primary;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}