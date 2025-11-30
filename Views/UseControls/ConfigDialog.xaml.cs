using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;
using ToolVip.ViewModels.Pages;

namespace ToolVip.Views.UseControls
{
    // [CẬP NHẬT] Sử dụng namespace đầy đủ theo yêu cầu
    public partial class ConfigDialog : System.Windows.Controls.UserControl
    {
        // Import hàm API để kiểm tra trạng thái phím toàn cục (Global)
        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);

        private const int VK_MENU = 0x12;  // Mã phím ALT
        private const int VK_1 = 0x31;     // Mã phím 1
        private const int VK_2 = 0x32;     // Mã phím 2

        private DispatcherTimer _timer;

        public ConfigDialog(AutoViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            this.Loaded += ConfigDialog_Loaded;
            this.Unloaded += ConfigDialog_Unloaded;
        }

        private void ConfigDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Khởi tạo Timer quét phím mỗi 50ms
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(50);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void ConfigDialog_Unloaded(object sender, RoutedEventArgs e)
        {
            // Dừng Timer khi đóng Dialog để tiết kiệm tài nguyên
            _timer?.Stop();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            var viewModel = DataContext as AutoViewModel;
            if (viewModel == null) return;

            // Kiểm tra trạng thái phím (Bit 0x8000 nghĩa là phím đang được nhấn)
            // Cách này bắt phím ngay cả khi ứng dụng không có Focus
            bool isAltDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            bool is1Down = (GetAsyncKeyState(VK_1) & 0x8000) != 0;
            bool is2Down = (GetAsyncKeyState(VK_2) & 0x8000) != 0;

            if (isAltDown)
            {
                if (is1Down)
                {
                    // Gọi lệnh cập nhật X1, Y1 (Alt + 1)
                    viewModel.GetCoordinateACommand.Execute(null);
                }
                else if (is2Down)
                {
                    // Gọi lệnh cập nhật X2, Y2 (Alt + 2)
                    viewModel.GetCoordinateSCommand.Execute(null);
                }
            }
        }
    }
}