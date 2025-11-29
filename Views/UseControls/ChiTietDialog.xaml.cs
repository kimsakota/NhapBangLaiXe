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

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

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
                        // [FIX 3] Thử set Clipboard nhiều lần để tránh lỗi crash do xung đột
                        bool copySuccess = false;
                        for (int i = 0; i < 5; i++)
                        {
                            try
                            {
                                System.Windows.Clipboard.SetText(textToPaste);
                                copySuccess = true;
                                break;
                            }
                            catch { await Task.Delay(50); } // Chờ chút rồi thử lại
                        }

                        if (!copySuccess)
                        {
                            System.Windows.MessageBox.Show("Không thể copy vào Clipboard (đang bị ứng dụng khác chiếm giữ).", "Lỗi");
                            return;
                        }

                        // 2. Kiểm tra lại Handle giả lập
                        if (_cachedEmulatorHandle == IntPtr.Zero || !IsWindow(_cachedEmulatorHandle))
                        {
                            // [FIX 2] Tìm kiếm linh hoạt hơn (Contains đã có trong code cũ, nhưng hãy đảm bảo tên đúng)
                            _cachedEmulatorHandle = FindEmulatorWindow("LDPlayer");
                        }

                        // Nếu tìm thấy giả lập
                        if (_cachedEmulatorHandle != IntPtr.Zero)
                        {
                            // A. Mẹo Focus: Chuyển sang Shell trước để "reset" trạng thái focus
                            IntPtr shellHandle = GetShellWindow();
                            if (shellHandle != IntPtr.Zero)
                            {
                                SetForegroundWindow(shellHandle);
                                await Task.Delay(50); // [FIX 1] Tăng delay lên 50ms
                            }

                            // B. Chuyển Focus sang Giả lập
                            if (IsIconic(_cachedEmulatorHandle)) ShowWindow(_cachedEmulatorHandle, SW_RESTORE);

                            // Cố gắng SetForeground nhiều lần (Windows 10/11 rất chặt việc này)
                            SetForegroundWindow(_cachedEmulatorHandle);

                            /*// [FIX 1] TĂNG THỜI GIAN CHỜ LÊN 150ms
                            // Để đảm bảo cửa sổ LDPlayer đã thực sự nổi lên trước khi bấm phím
                            await Task.Delay(150);

                            // Chờ 1 chút để chắc chắn giả lập đã nhận focus
                            await Task.Delay(50); // Có thể tăng lên 100 nếu máy chậm*/

                            // THAY THẾ SendKeys BẰNG ĐOẠN NÀY:
                            // 1. Nhấn giữ Ctrl
                            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                            // 2. Nhấn V
                            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                            // 3. Nhả V
                            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                            // 4. Nhả Ctrl
                            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        }
                        else
                        {
                            // Nếu không tìm thấy thì báo nhẹ để biết
                            Debug.WriteLine("Không tìm thấy cửa sổ LDPlayer nào.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _cachedEmulatorHandle = IntPtr.Zero;
                        Debug.WriteLine("Lỗi dán: " + ex.Message);
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