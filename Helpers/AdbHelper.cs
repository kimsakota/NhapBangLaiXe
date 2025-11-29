using System.Diagnostics;
using System.IO;

namespace ToolVip.Helpers
{
    public static class AdbHelper
    {
        // QUAN TRỌNG: Đường dẫn đến file adb.exe của LDPlayer
        // Bạn hãy kiểm tra lại ổ C của bạn xem đúng đường dẫn này chưa nhé
        private static string _adbPath = @"E:\LDPlayer\LDPlayer9\adb.exe";

        /// <summary>
        /// Gửi văn bản vào Android (Thay thế SendKeys)
        /// </summary>
        public static void SendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // Xử lý ký tự đặc biệt cho ADB
            // 1. ADB không hiểu dấu cách (space), phải thay bằng %s
            // 2. Ký tự " phải thêm dấu \ đằng trước
            string safeText = text
                .Replace(" ", "%s")
                .Replace("\"", "\\\"")
                .Replace("'", "\\'")
                .Replace("&", "\\&")
                .Replace("(", "\\(")
                .Replace(")", "\\)");

            // Gửi lệnh: input text "noidung"
            RunAdbCommand($"shell input text \"{safeText}\"");
        }

        public static void Paste()
        {
            // Mã 279 là KEYCODE_PASTE của Android
            RunAdbCommand("shell input keyevent 279");
        }

        /// <summary>
        /// Hàm chạy lệnh ADB ngầm (không hiện cửa sổ đen)
        /// </summary>
        private static void RunAdbCommand(string arguments)
        {
            try
            {
                if (!File.Exists(_adbPath))
                {
                    // Nếu không tìm thấy adb của LDPlayer, thử dùng adb mặc định của Windows (nếu có)
                    // Hoặc bạn có thể throw exception để báo lỗi
                    _adbPath = "adb";
                }

                var p = new Process();
                p.StartInfo.FileName = _adbPath;
                p.StartInfo.Arguments = arguments;

                // Cấu hình để chạy ẩn hoàn toàn
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;

                p.Start();
                p.WaitForExit();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Lỗi ADB: " + ex.Message);
            }
        }
    }
}