using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ToolVip.Models;

namespace ToolVip.Services
{
    public interface IRecordService
    {
        void StartRecording();
        void StopRecording();
        Task PlayRecordingAsync(CancellationToken token);
        bool IsRecording { get; }
    }

    public class RecordService : IRecordService
    {
        private List<MacroEvent> _events = new();
        private Stopwatch _stopwatch = new(); // Đồng hồ bấm giờ độ phân giải cao
        private long _lastEventTime = 0;      // Mốc thời gian của sự kiện trước đó
        
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelMouseProc _proc;
        private readonly string _filePath;

        public bool IsRecording { get; private set; } = false;

        public RecordService()
        {
            _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "macro_record.json");
            _proc = HookCallback; 
        }

        // --- GHI (RECORD) ---
        public void StartRecording()
        {
            _events.Clear();
            _stopwatch.Restart(); 
            _lastEventTime = 0; // Reset mốc thời gian
            _hookId = SetHook(_proc);
            IsRecording = true;
        }

        public void StopRecording()
        {
            UnhookWindowsHookEx(_hookId);
            IsRecording = false;
            _stopwatch.Stop();

            // Loại bỏ thao tác click cuối cùng (nút Stop) để tránh vòng lặp vô tận
            RemoveLastClick();

            // Lưu file
            try 
            {
                var json = JsonSerializer.Serialize(_events, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_filePath, json);
            }
            catch { /* Xử lý lỗi nếu cần */ }
        }
        
        private void RemoveLastClick()
        {
             if (_events.Count > 0 && _events.Last().Type == MacroEventType.LeftUp)
             {
                 _events.RemoveAt(_events.Count - 1);
                 if (_events.Count > 0 && _events.Last().Type == MacroEventType.LeftDown)
                     _events.RemoveAt(_events.Count - 1);
             }
        }

        // --- PHÁT LẠI (PLAY) - LOGIC "XỊN XÒ" ---
        public async Task PlayRecordingAsync(CancellationToken token)
        {
            if (!File.Exists(_filePath)) return;

            List<MacroEvent>? events;
            try
            {
                var json = await File.ReadAllTextAsync(_filePath, token);
                events = JsonSerializer.Deserialize<List<MacroEvent>>(json);
            }
            catch { return; }

            if (events == null || events.Count == 0) return;

            // Dùng Stopwatch để đồng bộ thời gian thực
            var stopwatch = Stopwatch.StartNew();
            double accumulatedTime = 0; // Thời gian tích lũy lý thuyết

            foreach (var evt in events)
            {
                if (token.IsCancellationRequested) break;

                // 1. Tính toán thời điểm sự kiện này CẦN PHẢI xảy ra
                accumulatedTime += evt.Delay;
                
                // 2. Chờ cho đến đúng thời điểm đó
                // Kỹ thuật Hybrid Wait: Kết hợp Task.Delay và SpinWait
                while (true)
                {
                    if (token.IsCancellationRequested) break;

                    double remaining = accumulatedTime - stopwatch.ElapsedMilliseconds;

                    // Nếu đã đến giờ (hoặc trễ giờ), thực hiện ngay
                    if (remaining <= 0) break; 

                    // Nếu còn chờ lâu (> 15ms), dùng Task.Delay để nhường CPU
                    if (remaining > 15)
                    {
                        await Task.Delay(1, token); 
                    }
                    else
                    {
                        // Nếu còn ít thời gian (< 15ms), dùng vòng lặp (Spin) để canh chính xác từng giây
                        // Không làm gì cả, chỉ quay vòng lặp chờ
                        Thread.SpinWait(100); 
                    }
                }

                // 3. Thực hiện hành động
                SetCursorPos(evt.X, evt.Y);
                PerformMouseAction(evt);
            }

            stopwatch.Stop();
        }

        private void PerformMouseAction(MacroEvent evt)
        {
            int flags = 0;
            switch (evt.Type)
            {
                case MacroEventType.LeftDown: flags = MOUSEEVENTF_LEFTDOWN; break;
                case MacroEventType.LeftUp: flags = MOUSEEVENTF_LEFTUP; break;
                case MacroEventType.RightDown: flags = MOUSEEVENTF_RIGHTDOWN; break;
                case MacroEventType.RightUp: flags = MOUSEEVENTF_RIGHTUP; break;
                case MacroEventType.Scroll: flags = MOUSEEVENTF_WHEEL; break;
            }
            mouse_event(flags, 0, 0, evt.MouseData, 0);
        }

        // --- HOOK CALLBACK (XỬ LÝ KHI GHI) ---
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsRecording)
            {
                int msg = wParam.ToInt32();
                
                // Chỉ xử lý các sự kiện chuột quan trọng
                if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP || 
                    msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP || 
                    msg == WM_MOUSEWHEEL)
                {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                    long currentTime = _stopwatch.ElapsedMilliseconds;
                    int delay = (int)(currentTime - _lastEventTime);
                    _lastEventTime = currentTime; // Cập nhật mốc thời gian

                    var evt = new MacroEvent
                    {
                        X = hookStruct.pt.x,
                        Y = hookStruct.pt.y,
                        Delay = delay, // Lưu khoảng thời gian trôi qua từ sự kiện trước
                        MouseData = 0
                    };

                    bool valid = true;
                    switch (msg)
                    {
                        case WM_LBUTTONDOWN: evt.Type = MacroEventType.LeftDown; break;
                        case WM_LBUTTONUP: evt.Type = MacroEventType.LeftUp; break;
                        case WM_RBUTTONDOWN: evt.Type = MacroEventType.RightDown; break;
                        case WM_RBUTTONUP: evt.Type = MacroEventType.RightUp; break;
                        case WM_MOUSEWHEEL:
                            evt.Type = MacroEventType.Scroll;
                            short delta = (short)((hookStruct.mouseData >> 16) & 0xFFFF);
                            evt.MouseData = delta;
                            break;
                        default: valid = false; break;
                    }

                    if (valid) _events.Add(evt);
                }
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        // --- WIN32 API ---
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
        [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
        [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("user32.dll")] private static extern void mouse_event(int dwFlags, int dx, int dy, int dwData, int dwExtraInfo);
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int X, int Y);

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEWHEEL = 0x020A;
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_WHEEL = 0x0800;

        [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x; public int y; }
        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        private IntPtr SetHook(LowLevelMouseProc proc)
        {
            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule!)
            {
                return SetWindowsHookEx(WH_MOUSE_LL, proc, GetModuleHandle(curModule.ModuleName), 0);
            }
        }
    }
}