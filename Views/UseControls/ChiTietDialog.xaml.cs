using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows; // Đảm bảo có using này cho RoutedEventArgs
using System.Windows.Controls;
using System.Windows.Input;
using ToolVip.Models;

namespace ToolVip.Views.UseControls
{
    public partial class ChiTietDialog : System.Windows.Controls.UserControl
    {
        // --- CÁC HÀM API WIN32 GIỮ NGUYÊN ---
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
        private const int SW_RESTORE = 9;

        private IntPtr _cachedEmulatorHandle = IntPtr.Zero;

        public ChiTietDialog()
        {
            InitializeComponent();
        }

        // [THAY ĐỔI] Đổi tên hàm và tham số sự kiện từ MouseButtonEventArgs -> RoutedEventArgs
        private async void OnButtonClick(object sender, RoutedEventArgs e)
        {
            e.Handled = true;

            if (sender is System.Windows.Controls.Button btn && btn.DataContext is DriverProfile profile)
            {
                // Logic dán giữ nguyên như cũ
                string textToPaste = GetTextFromButton(btn.Uid, profile);
                string cleanText = SanitizeText(textToPaste, btn.Uid);
                if (!string.IsNullOrEmpty(cleanText))
                {
                    try
                    {
                        // Copy vào Clipboard
                        bool copySuccess = true;
                        
                        System.Windows.Clipboard.SetText(cleanText);
                            

                        if (!copySuccess)
                        {
                            System.Windows.MessageBox.Show("Lỗi Clipboard", "Lỗi");
                            return;
                        }

                        // Tìm và focus cửa sổ giả lập
                        if (_cachedEmulatorHandle == IntPtr.Zero || !IsWindow(_cachedEmulatorHandle))
                        {
                            _cachedEmulatorHandle = FindEmulatorWindow("LDPlayer");
                        }

                        if (_cachedEmulatorHandle != IntPtr.Zero)
                        {
                            // Mẹo Focus
                            IntPtr shellHandle = GetShellWindow();
                            if (shellHandle != IntPtr.Zero)
                            {
                                SetForegroundWindow(shellHandle);
                                await Task.Delay(50);
                            }

                            if (IsIconic(_cachedEmulatorHandle)) ShowWindow(_cachedEmulatorHandle, SW_RESTORE);
                            SetForegroundWindow(_cachedEmulatorHandle);

                            // Gửi phím Ctrl + V
                            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
                            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
                            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                        }
                        else
                        {
                            Debug.WriteLine("Không tìm thấy cửa sổ LDPlayer.");
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

        private string GetTextFromButton(string uid, DriverProfile profile)
        {
            // [GIỮ NGUYÊN CODE CŨ]
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

        private IntPtr FindEmulatorWindow(string titlePart)
        {
            // [GIỮ NGUYÊN CODE CŨ]
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

        // [HÀM MỚI] Lọc bỏ ký tự đặc biệt gây lỗi
        private string SanitizeText(string input, string uid)
        {
            if (string.IsNullOrEmpty(input)) return "";

            string text = input.Trim();

            // Xóa sạch các ký tự điều khiển (Null, Tab, Enter...) - Nguyên nhân chính gây lỗi ô vuông
            text = Regex.Replace(text, @"\p{C}+", "");

            // Xử lý kỹ hơn cho từng loại dữ liệu
            switch (uid)
            {
                case "2": // CCCD
                case "4": // Số điện thoại
                case "9": // Số máy
                case "10": // Số khung
                    // Chỉ cho phép Số và Chữ cái (A-Z, 0-9). Xóa hết dấu chấm, phẩy, khoảng trắng thừa
                    text = Regex.Replace(text, @"[^a-zA-Z0-9]", "");
                    break;

                case "3": // Ngày tháng (IssueDate)
                          // Chỉ giữ lại số và dấu gạch chéo/ngang
                    text = Regex.Replace(text, @"[^0-9/\-]", "");
                    break;

                default:
                    // Họ tên, Địa chỉ: Giữ nguyên (chỉ xóa ký tự điều khiển ở trên)
                    break;
            }
            return text;
        }
    }
    //1
}