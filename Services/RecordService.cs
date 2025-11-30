using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using ToolVip.Helpers;
using ToolVip.Models;

namespace ToolVip.Services
{
    public interface IRecordService
    {
        void StartRecording();
        List<MacroEvent> StopRecordingAndGet();
        Task PlayRecordingAsync(List<MacroEvent> events, CancellationToken token);
        void SaveRecording(List<MacroEvent> events, string filePath);
        bool IsRecording { get; }
    }

    public class RecordService : IRecordService
    {
        private List<MacroEvent> _events = new();
        private Stopwatch _stopwatch = new();
        private long _lastEventTime = 0;

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelMouseProc _proc;

        // Giảm delay tối thiểu xuống 1ms để chuyển động mượt và giống thực tế nhất
        private const int MIN_DELAY_MS = 1;
        private const int MIN_MOUSE_MOVE_DISTANCE = 0; // Ghi lại tất cả chuyển động nhỏ nhất

        private int _lastX = 0;
        private int _lastY = 0;

        public bool IsRecording { get; private set; } = false;

        public RecordService()
        {
            _proc = HookCallback;
        }

        public void StartRecording()
        {
            _events.Clear();
            _stopwatch.Restart();
            _lastEventTime = 0;
            _hookId = SetHook(_proc);
            IsRecording = true;
        }

        public List<MacroEvent> StopRecordingAndGet()
        {
            if (!IsRecording) return new List<MacroEvent>();

            UnhookWindowsHookEx(_hookId);
            IsRecording = false;
            _stopwatch.Stop();

            RemoveLastClick();
            // Bỏ NormalizeEvents hoặc giảm thiểu can thiệp để giữ độ chính xác của thao tác
            // NormalizeEvents(); 

            return new List<MacroEvent>(_events);
        }

        public void SaveRecording(List<MacroEvent> events, string filePath)
        {
            try
            {
                var dir = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var options = new JsonSerializerOptions { WriteIndented = true };
                var json = JsonSerializer.Serialize(events, options);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Lỗi lưu file macro: {ex.Message}");
            }
        }

        private void RemoveLastClick()
        {
            // Xóa sự kiện click Stop
            if (_events.Count > 0 && _events.Last().Type == MacroEventType.LeftUp)
            {
                _events.RemoveAt(_events.Count - 1);
                if (_events.Count > 0 && _events.Last().Type == MacroEventType.LeftDown)
                    _events.RemoveAt(_events.Count - 1);
            }
        }

        public async Task PlayRecordingAsync(List<MacroEvent> events, CancellationToken token)
        {
            if (events == null || events.Count == 0) return;

            var stopwatch = Stopwatch.StartNew();
            double accumulatedTime = 0;

            foreach (var evt in events)
            {
                CheckStopHotkey();
                if (token.IsCancellationRequested) break;

                accumulatedTime += evt.Delay;

                // Cơ chế chờ chính xác (High precision wait)
                while (true)
                {
                    CheckStopHotkey();
                    if (token.IsCancellationRequested) return;

                    double elapsed = stopwatch.ElapsedMilliseconds;
                    double remaining = accumulatedTime - elapsed;

                    if (remaining <= 0) break;

                    // Nếu còn nhiều thời gian thì Delay, ít thì SpinWait để chính xác
                    if (remaining > 15) await Task.Delay(10, token);
                    else Thread.SpinWait(100);
                }

                // 1. Di chuyển chuột (SetCursorPos di chuyển chuột tuyệt đối)
                SetCursorPos(evt.X, evt.Y);

                // 2. Nếu là Click, thực hiện lệnh click
                if (evt.Type != MacroEventType.MouseMove)
                {
                    // Delay cực ngắn để đảm bảo tọa độ đã được cập nhật trước khi click
                    await Task.Delay(1, token);
                    PerformMouseAction(evt);
                }
            }
            stopwatch.Stop();
        }

        private void CheckStopHotkey()
        {
            // Kiểm tra phím Ctrl + S
            bool isCtrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
            bool isSDown = (GetAsyncKeyState(VK_S) & 0x8000) != 0;

            if (isCtrlDown && isSDown)
            {
                throw new OperationCanceledException("User stop by Ctrl+S");
            }
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
                // MouseMove đã được xử lý bởi SetCursorPos ở ngoài
                case MacroEventType.MouseMove: return;
            }

            // Gửi lệnh click
            mouse_event(flags, 0, 0, evt.MouseData, 0);
        }

        // --- HOOK CALLBACK ---
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && IsRecording)
            {
                int msg = wParam.ToInt32();
                // Chỉ bắt các sự kiện chuột cơ bản
                if (msg == WM_LBUTTONDOWN || msg == WM_LBUTTONUP ||
                    msg == WM_RBUTTONDOWN || msg == WM_RBUTTONUP ||
                    msg == WM_MOUSEWHEEL || msg == WM_MOUSEMOVE)
                {
                    MSLLHOOKSTRUCT hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                    long currentTime = _stopwatch.ElapsedMilliseconds;
                    int delay = (int)(currentTime - _lastEventTime);

                    // Bỏ qua lọc khoảng cách để ghi lại chuyển động mượt mà hơn
                    if (msg == WM_MOUSEMOVE)
                    {
                        // Lưu ý: Nếu ghi quá chi tiết file sẽ nặng, nhưng sẽ mượt
                        // Nếu muốn tối ưu có thể bật lại lọc khoảng cách nhỏ
                    }

                    _lastEventTime = currentTime;
                    _lastX = hookStruct.pt.x;
                    _lastY = hookStruct.pt.y;

                    var evt = new MacroEvent
                    {
                        X = hookStruct.pt.x,
                        Y = hookStruct.pt.y,
                        Delay = delay,
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
                        case WM_MOUSEMOVE: evt.Type = MacroEventType.MouseMove; break;
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
        [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vKey);

        private const int WH_MOUSE_LL = 14;
        private const int WM_MOUSEMOVE = 0x0200;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MOUSEWHEEL = 0x020A;

        private const int MOUSEEVENTF_MOVE = 0x0001;
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;
        private const int MOUSEEVENTF_RIGHTDOWN = 0x08;
        private const int MOUSEEVENTF_RIGHTUP = 0x10;
        private const int MOUSEEVENTF_WHEEL = 0x0800;

        private const int VK_CONTROL = 0x11;
        private const int VK_S = 0x53;

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