using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using ToolVip.Models;

namespace ToolVip.Views.UseControls
{
    public partial class ChiTietDialog : System.Windows.Controls.UserControl
    {
        // --- API WINDOWS (Win32) ---
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd); // Kiểm tra cửa sổ còn tồn tại không

        private const int SW_RESTORE = 9;

        // BIẾN LƯU TRỮ CỬA SỔ GIẢ LẬP (CACHE)
        private IntPtr _cachedEmulatorHandle = IntPtr.Zero;

        public ChiTietDialog()
        {
            InitializeComponent();
        }

        private async void OnButtonDoubleClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;

            if (sender is System.Windows.Controls.Button btn && btn.DataContext is DriverProfile profile)
            {
                // 1. Lấy dữ liệu
                string textToPaste = GetTextFromButton(btn.Uid, profile);

                if (!string.IsNullOrEmpty(textToPaste))
                {
                    try
                    {
                        // 2. Ghi vào Clipboard
                        System.Windows.Clipboard.SetText(textToPaste);

                        // 3. Lấy Handle giả lập (Dùng Cache để tăng tốc)
                        // Nếu chưa có cache hoặc cửa sổ đã bị tắt -> Tìm lại
                        if (_cachedEmulatorHandle == IntPtr.Zero || !IsWindow(_cachedEmulatorHandle))
                        {
                            _cachedEmulatorHandle = FindEmulatorWindow("LDPlayer");
                        }

                        // Nếu tìm thấy giả lập
                        if (_cachedEmulatorHandle != IntPtr.Zero)
                        {
                            // === KỸ THUẬT FOCUS TOGGLE (TỐI ƯU TỐC ĐỘ) ===

                            // A. Chuyển Focus sang Shell (Cực nhanh)
                            IntPtr shellHandle = GetShellWindow();
                            if (shellHandle != IntPtr.Zero)
                            {
                                SetForegroundWindow(shellHandle);
                                await Task.Delay(10); // Chỉ chờ 10ms để kích hoạt sự kiện mất focus
                            }

                            // B. Chuyển Focus lại Giả lập
                            if (IsIconic(_cachedEmulatorHandle)) ShowWindow(_cachedEmulatorHandle, SW_RESTORE);
                            SetForegroundWindow(_cachedEmulatorHandle);

                            // Chờ 20ms để giả lập đồng bộ (Con số thấp nhất an toàn)
                            await Task.Delay(20);

                            // 4. Gửi lệnh Paste
                            System.Windows.Forms.SendKeys.SendWait("^{v}");
                        }
                    }
                    catch (Exception ex)
                    {
                        // Nếu lỗi, xóa cache để lần sau tìm lại
                        _cachedEmulatorHandle = IntPtr.Zero;
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
        }

        // Hàm helper lấy dữ liệu
        private string GetTextFromButton(string uid, DriverProfile profile)
        {
            switch (uid)
            {
                case "1": return profile.FullName;
                case "2": return profile.Cccd;
                case "3": return profile.IssueDate;
                case "4": return profile.PhoneNumber;
                case "5": return profile.Address;
                case "6": return profile.WardCommune;
                case "7": return profile.WardCommune;
                case "8": return profile.LicensePlate;
                case "9": return profile.EngineNumber;
                case "10": return profile.ChassisNumber;
                default: return "";
            }
        }

        // Tìm kiếm cửa sổ (Chỉ chạy 1 lần đầu tiên)
        private IntPtr FindEmulatorWindow(string titlePart)
        {
            foreach (Process p in Process.GetProcesses())
            {
                if (!string.IsNullOrEmpty(p.MainWindowTitle) &&
                    p.MainWindowTitle.Contains(titlePart, StringComparison.OrdinalIgnoreCase))
                {
                    return p.MainWindowHandle;
                }
            }
            return IntPtr.Zero;
        }


    }
}       