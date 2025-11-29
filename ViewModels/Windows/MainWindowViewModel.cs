using System.Collections.ObjectModel;
using Wpf.Ui.Controls;

namespace ToolVip.ViewModels.Windows
{
    public partial class MainWindowViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _applicationTitle = "WPF UI - ToolVip";

        [ObservableProperty]
        private ObservableCollection<object> _menuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Home",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Person24 },
                TargetPageType = typeof(Views.Pages.DashboardPage)
            },
            new NavigationViewItem()
            {
                Content = "Đã lưu",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Save24 }, // Icon Save
                TargetPageType = typeof(Views.Pages.SavedDataPage)
            },
            new NavigationViewItem()
            {
                Content = "Nhập",
                Icon = new SymbolIcon { Symbol = SymbolRegular.ClipboardPaste24 }, // Icon Clipboard
                TargetPageType = typeof(Views.Pages.ImportPage)
            },

            new NavigationViewItemSeparator(), // Đường kẻ phân cách

            

        };

        [ObservableProperty]
        private ObservableCollection<object> _footerMenuItems = new()
        {
            new NavigationViewItem()
            {
                Content = "Settings",
                Icon = new SymbolIcon { Symbol = SymbolRegular.Settings24 },
                TargetPageType = typeof(Views.Pages.SettingsPage)
            }
        };

        [ObservableProperty]
        private ObservableCollection<MenuItem> _trayMenuItems = new()
        {
            new MenuItem { Header = "Home", Tag = "tray_home" }
        };
    }
}
