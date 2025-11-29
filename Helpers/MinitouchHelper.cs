using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ToolVip.Helpers
{
    public class ScrcpyHelper
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        // Đường dẫn ADB (cập nhật lại theo máy bạn)
        private string _adbPath = @"E:\LDPlayer\LDPlayer9\adb.exe";
        private const int PORT = 27183; // Port ngẫu nhiên để tránh đụng hàng

        // Scrcpy cần biết kích thước màn hình để tính toán tọa độ chuẩn
        private int _screenWidth = 0;
        private int _screenHeight = 0;

        public bool IsConnected { get; private set; } = false;

        // Hàm khởi động Scrcpy Server
        public bool Start()
        {
            if (IsConnected && _client != null && _client.Connected)
                return true;

            try
            {
                // 0. Lấy độ phân giải màn hình trước (Bắt buộc với Scrcpy)
                GetScreenResolution();

                // 1. Push file server vào điện thoại
                string serverPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scrcpy-server.jar");
                if (!File.Exists(serverPath))
                {
                    Debug.WriteLine("Thiếu file scrcpy-server.jar!");
                    return false;
                }
                RunAdbCommand($"push \"{serverPath}\" /data/local/tmp/scrcpy-server.jar");

                // 2. Map port (Forward)
                RunAdbCommand($"forward tcp:{PORT} localabstract:scrcpy");

                // 3. Chạy Server trên Android (Chạy ngầm)
                // Tham số này dành cho scrcpy v1.24: tắt video, tắt audio, chỉ bật control
                string cmd = "shell CLASSPATH=/data/local/tmp/scrcpy-server.jar app_process / com.genymobile.scrcpy.Server 1.24 tunnel_forward=true control=true display_id=0 audio=false show_touches=false max_size=800";

                Thread thread = new Thread(() =>
                {
                    RunAdbCommand(cmd);
                });
                thread.IsBackground = true;
                thread.Start();

                // Đợi server khởi động
                Thread.Sleep(1000);

                // 4. Kết nối Socket
                _client = new TcpClient("127.0.0.1", PORT);
                _stream = _client.GetStream();

                // 5. Đọc byte dummy đầu tiên (Scrcpy gửi 1 byte để báo hiệu connect thành công)
                _stream.ReadByte();

                // 6. Gửi Device Name (Protocol v1.24 yêu cầu client gửi tên thiết bị, tối đa 64 bytes)
                byte[] deviceName = new byte[64];
                byte[] nameBytes = Encoding.ASCII.GetBytes("ToolVip");
                Array.Copy(nameBytes, deviceName, Math.Min(nameBytes.Length, 64));
                _stream.Write(deviceName, 0, 64);

                IsConnected = true;
                Debug.WriteLine($"Scrcpy Connected! Res: {_screenWidth}x{_screenHeight}");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Lỗi Start Scrcpy: " + ex.Message);
                IsConnected = false;
                return false;
            }
        }

        public void Stop()
        {
            try
            {
                _client?.Close();
                RunAdbCommand("shell pkill app_process"); // Kill server
            }
            catch { }
            IsConnected = false;
        }

        // --- CÁC HÀM ĐIỀU KHIỂN (Protocol v1.24) ---

        public void Tap(int x, int y)
        {
            if (!CheckConnection()) return;

            // Nhấn xuống (Down)
            InjectTouchEvent(0, x, y); // Action 0 = Down
            Thread.Sleep(50); // Giữ 50ms
            // Nhả ra (Up)
            InjectTouchEvent(1, x, y); // Action 1 = Up
        }

        public void Swipe(int x1, int y1, int x2, int y2, int durationMs = 300)
        {
            if (!CheckConnection()) return;

            // Down tại điểm đầu
            InjectTouchEvent(0, x1, y1);

            int steps = durationMs / 10;
            float dx = (x2 - x1) / (float)steps;
            float dy = (y2 - y1) / (float)steps;

            for (int i = 1; i <= steps; i++)
            {
                int nextX = (int)(x1 + dx * i);
                int nextY = (int)(y1 + dy * i);
                // Move (Action 2)
                InjectTouchEvent(2, nextX, nextY);
                Thread.Sleep(5);
            }

            // Up tại điểm cuối
            InjectTouchEvent(1, x2, y2);
        }

        /// <summary>
        /// Hàm đóng gói dữ liệu Binary theo chuẩn Scrcpy v1.24
        /// Tổng cộng 28-32 bytes tùy cấu trúc
        /// </summary>
        private void InjectTouchEvent(int action, int x, int y)
        {
            try
            {
                // Cấu trúc gói tin INJECT_TOUCH_EVENT (v1.24):
                // [Type: 1] [Action: 1] [PointerId: 8] [X: 4] [Y: 4] [Width: 2] [Height: 2] [Pressure: 2] [Buttons: 4]

                using (var ms = new MemoryStream())
                using (var writer = new BinaryWriter(ms))
                {
                    writer.Write((byte)2); // Type 2: INJECT_TOUCH_EVENT
                    writer.Write((byte)action); // 0: Down, 1: Up, 2: Move
                    writer.Write((long)-1); // PointerId (-1 là chuột/ngón tay mặc định)

                    writer.Write(ToBigEndian(x)); // X (4 bytes)
                    writer.Write(ToBigEndian(y)); // Y (4 bytes)

                    writer.Write(ToBigEndian((short)_screenWidth));  // Width (2 bytes)
                    writer.Write(ToBigEndian((short)_screenHeight)); // Height (2 bytes)

                    writer.Write(ToBigEndian(unchecked((short)0xFFFF))); // Pressure (Lực nhấn max)
                    writer.Write(ToBigEndian(1)); // Buttons (1 = Primary/Left Click)

                    // Gửi đi
                    byte[] packet = ms.ToArray();
                    _stream?.Write(packet, 0, packet.Length);
                    _stream?.Flush();
                }
            }
            catch (Exception)
            {
                IsConnected = false;
            }
        }

        // Helper: Scrcpy dùng Big Endian (Network Byte Order), C# dùng Little Endian -> Cần đảo byte
        private int ToBigEndian(int value) => System.Net.IPAddress.HostToNetworkOrder(value);
        private short ToBigEndian(short value) => (short)System.Net.IPAddress.HostToNetworkOrder(value);
        private long ToBigEndian(long value) => System.Net.IPAddress.HostToNetworkOrder(value);

        private bool CheckConnection()
        {
            if (!IsConnected) return Start();
            return true;
        }

        private void GetScreenResolution()
        {
            // Chạy lệnh: adb shell wm size
            // Output: "Physical size: 1080x1920"
            string output = RunAdbCommandWithOutput("shell wm size");
            var match = Regex.Match(output, @"(\d+)x(\d+)");
            if (match.Success)
            {
                _screenWidth = int.Parse(match.Groups[1].Value);
                _screenHeight = int.Parse(match.Groups[2].Value);
            }
            else
            {
                // Fallback nếu không lấy được
                _screenWidth = 720;
                _screenHeight = 1280;
            }
        }

        private void RunAdbCommand(string arguments)
        {
            var p = new Process();
            p.StartInfo.FileName = _adbPath;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            p.Start();
            p.WaitForExit();
        }

        private string RunAdbCommandWithOutput(string arguments)
        {
            var p = new Process();
            p.StartInfo.FileName = _adbPath;
            p.StartInfo.Arguments = arguments;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            return output;
        }
    }
}