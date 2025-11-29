using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ToolVip.Helpers
{
    public class MinitouchHelper
    {
        private TcpClient? _client;
        private NetworkStream? _stream;
        private Process? _minitouchProcess;
        private string _adbPath = @"E:\LDPlayer\LDPlayer9\adb.exe"; // Kiểm tra đường dẫn
        private const int PORT = 1111; // Port giao tiếp
        public bool IsConnected { get; private set; } = false;

        // Hàm khởi động Minitouch
        public bool Start()
        {
            if(IsConnected && _client != null && _client.Connected)
                return true;
            try
            {
                // 1. Map port PC (1111) -> Android (minitouch)
                RunAdbCommand($"forward tcp:{PORT} localabstract:minitouch");

                // 2. Chạy minitouch trên Android (Chạy ngầm)
                // Lưu ý: Cần thread riêng hoặc chạy không chờ để không treo UI
                Thread thread = new Thread(() =>
                {
                    RunAdbCommand("shell /data/local/tmp/minitouch");
                });
                thread.IsBackground = true;
                thread.Start();

                // Đợi 1 chút cho minitouch khởi động
                Thread.Sleep(500);

                // 3. Kết nối Socket
                _client = new TcpClient("127.0.0.1", PORT);
                _stream = _client.GetStream();

                // 4. Đọc Header (Minitouch gửi thông tin device ngay khi connect, ta cần đọc bỏ đi)
                byte[] buffer = new byte[1024];
                int bytesRead = _stream.Read(buffer, 0, buffer.Length);
                string header = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Debug.WriteLine("Minitouch Header: " + header);

                IsConnected = true;
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Lỗi Start Minitouch: " + ex.Message);
                IsConnected = false;
                return false;
            }
        }

        public void Stop()
        {
            _client?.Close();
            // Kill minitouch trên Android để đỡ tốn RAM
            RunAdbCommand("shell pkill minitouch");
            IsConnected = false;
        }

        // --- CÁC HÀM ĐIỀU KHIỂN ---

        public void Tap(int x, int y)
        {
            if (_stream == null) return;
            // Protocol:
            // d 0 x y 50 (Nhấn xuống contact 0, tọa độ x y, lực 50)
            // c (Commit - Thực thi)
            // u 0 (Nhả contact 0)
            // c (Commit)

            // 1. Nhấn xuống (Down)
            string cmdDown = $"d 0 {x} {y} 50\nc\n";
            SendRaw(cmdDown);

            Thread.Sleep(50);

            // 2. Nhả ra (Up)
            string cmdUp = "u 0\nc\n";
            SendRaw(cmdUp);
        }

        public void Swipe(int x1, int y1, int x2, int y2, int durationMs = 300)
        {
            if (_stream == null) return;

            // Nhấn xuống tại điểm đầu
            SendRaw($"d 0 {x1} {y1} 50\nc\n");

            // Tính toán bước nhảy để vuốt mượt (chia nhỏ quãng đường)
            int steps = durationMs / 10; // 10ms một bước
            float dx = (x2 - x1) / (float)steps;
            float dy = (y2 - y1) / (float)steps;

            for (int i = 1; i <= steps; i++)
            {
                int nextX = (int)(x1 + dx * i);
                int nextY = (int)(y1 + dy * i);
                // Lệnh m (Move)
                SendRaw($"m 0 {nextX} {nextY} 50\nc\n");
                Thread.Sleep(5); // Nghỉ cực ngắn để tạo độ mượt
            }

            // Nhả ra tại điểm cuối
            SendRaw($"d 0 {x2} {y2} 50\nc\nu 0\nc\n");
        }

        private void SendRaw(string cmd)
        {
            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(cmd);
                _stream?.Write(bytes, 0, bytes.Length);
            }
            catch { }
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
    }
}