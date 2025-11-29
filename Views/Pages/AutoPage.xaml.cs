using System.Globalization;
using System.Windows.Data;
using ToolVip.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace ToolVip.Views.Pages
{
    public partial class AutoPage : INavigableView<AutoViewModel>
    {
        public AutoViewModel ViewModel { get; }

        public AutoPage(AutoViewModel viewModel)
        {
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
        }
    }

    // --- CONVERTERS NHANH CHO UI ---

    public class IndexToVisibilityConverter : IValueConverter
    {
        public static IndexToVisibilityConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value?.ToString() == parameter?.ToString() ? Visibility.Visible : Visibility.Collapsed;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RunStateToStringConverter : IValueConverter
    {
        public static RunStateToStringConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? "DỪNG LẠI (Alt+S)" : "BẮT ĐẦU AUTO";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class RunStateToColorConverter : IValueConverter
    {
        public static RunStateToColorConverter Instance = new();
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (bool)value ? ControlAppearance.Danger : ControlAppearance.Success;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}