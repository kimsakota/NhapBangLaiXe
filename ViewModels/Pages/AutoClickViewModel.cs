using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ToolVip.Models;
using ToolVip.Services;
using Wpf.Ui.Controls; // Để dùng MessageBox hoặc Log

namespace ToolVip.ViewModels.Pages
{
    public partial class AutoClickViewModel : ObservableObject
    {
        private readonly IOcrService _ocrService;
        private CancellationTokenSource? _cts;

        // --- WIN32 API ---
        [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] private static extern void mouse_event(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
        private const int MOUSEEVENTF_LEFTDOWN = 0x02;
        private const int MOUSEEVENTF_LEFTUP = 0x04;

        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT lpPoint);
        [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }

        // --- [SỬA ĐỔI] Dùng STRING cho các ô nhập liệu để không bị lỗi khi xóa trắng ---
        [ObservableProperty] private string _x1 = "100";
        [ObservableProperty] private string _y1 = "100";
        [ObservableProperty] private string _x2 = "300";
        [ObservableProperty] private string _y2 = "200";

        [ObservableProperty] private string _keyword = "OK";
        [ObservableProperty] private bool _isExactMatch = true;

        // Cấu hình thêm bước (Dùng string luôn cho đồng bộ)
        [ObservableProperty] private string _newStepX = "0";
        [ObservableProperty] private string _newStepY = "0";
        [ObservableProperty] private string _newStepDelay = "1000";

        // Chế độ
        [ObservableProperty] private bool _isClickOCR = true;
        [ObservableProperty] private bool _isClickCustom = false;

        [ObservableProperty] private string _logText = "";
        [ObservableProperty] private string _currentMousePos = "X=0, Y=0";

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsNotRunning))]
        private bool _isRunning = false;

        public bool IsNotRunning => !IsRunning;

        // Danh sách kịch bản
        [ObservableProperty]
        private ObservableCollection<ClickStep> _scriptSteps = new();

        public AutoClickViewModel(IOcrService ocrService)
        {
            _ocrService = ocrService;

            // Task cập nhật tọa độ chuột liên tục
            Task.Run(async () =>
            {
                while (true)
                {
                    if (GetCursorPos(out POINT p))
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            CurrentMousePos = $"Chuột: {p.X}, {p.Y}");
                    }
                    await Task.Delay(100);
                }
            });
        }

        // Hàm tiện ích chuyển String về Int an toàn
        private int ParseInt(string input, int defaultValue = 0)
        {
            return int.TryParse(input, out int result) ? result : defaultValue;
        }

        [RelayCommand]
        private void AddStep()
        {
            int x = ParseInt(NewStepX);
            int y = ParseInt(NewStepY);
            int delay = ParseInt(NewStepDelay);

            var step = new ClickStep { X = x, Y = y, DelayMs = delay };
            ScriptSteps.Add(step);

            AppendLog($"[Script] Đã thêm bước: Click ({x}, {y})");
        }

        [RelayCommand]
        private void ClearSteps()
        {
            ScriptSteps.Clear();
            AppendLog("[Script] Đã xóa danh sách.");
        }

        [RelayCommand]
        private void ToggleRun()
        {
            if (IsRunning) StopAuto();
            else StartAuto();
        }

        [RelayCommand]
        private async Task TestOcr()
        {
            await ProcessOcrAsync(false);
        }

        private void StartAuto()
        {
            AppendLog(">>> BẮT ĐẦU AUTO...");
            var initResult = _ocrService.Init("vie");

            if (!initResult.Success)
            {
                AppendLog($"[LỖI] {initResult.Message}");
                System.Windows.MessageBox.Show(initResult.Message, "Lỗi");
                return;
            }

            IsRunning = true;
            _cts = new CancellationTokenSource();

            Task.Run(async () =>
            {
                while (_cts != null && !_cts.Token.IsCancellationRequested)
                {
                    await ProcessOcrAsync(true);
                    try { await Task.Delay(1000, _cts.Token); } catch { }
                }
            }, _cts.Token);
        }

        private void StopAuto()
        {
            IsRunning = false;
            _cts?.Cancel();
            _cts = null;
            AppendLog("<<< ĐÃ DỪNG.");
        }

        private async Task ProcessOcrAsync(bool isAutoMode)
        {
            // Chuyển đổi String sang Int để xử lý
            int x1 = ParseInt(X1);
            int y1 = ParseInt(Y1);
            int x2 = ParseInt(X2);
            int y2 = ParseInt(Y2);

            int x = Math.Min(x1, x2);
            int y = Math.Min(y1, y2);
            int w = Math.Abs(x2 - x1);
            int h = Math.Abs(y2 - y1);

            if (w < 5 || h < 5)
            {
                AppendLog($"[Lỗi Vùng] Kích thước quá nhỏ: {w}x{h}");
                return;
            }

            if (!isAutoMode) AppendLog($"[Scan] Quét vùng {x},{y} ({w}x{h})...");

            string text = await Task.Run(() => _ocrService.GetTextFromRegion(x, y, w, h));
            string cleanText = text.Replace("\n", " ").Trim();

            if (!isAutoMode) AppendLog($"[KQ] Đọc được: \"{cleanText}\"");

            bool isMatch = IsExactMatch
                ? cleanText.Equals(Keyword, StringComparison.OrdinalIgnoreCase)
                : cleanText.IndexOf(Keyword, StringComparison.OrdinalIgnoreCase) >= 0;

            if (isMatch && !string.IsNullOrWhiteSpace(Keyword))
            {
                AppendLog($"[TÌM THẤY] Từ khóa \"{Keyword}\"");

                if (isAutoMode)
                {
                    if (IsClickOCR)
                    {
                        int clickX = x + w / 2;
                        int clickY = y + h / 2;
                        PerformClick(clickX, clickY);
                        AppendLog($"-> Click tại chỗ: {clickX}, {clickY}");
                    }
                    else if (IsClickCustom)
                    {
                        if (ScriptSteps.Count == 0)
                        {
                            AppendLog("-> Kịch bản trống! Không click gì cả.");
                        }
                        else
                        {
                            AppendLog($"-> Chạy {ScriptSteps.Count} bước kịch bản...");
                            foreach (var step in ScriptSteps)
                            {
                                PerformClick(step.X, step.Y);
                                if (step.DelayMs > 0) await Task.Delay(step.DelayMs);
                            }
                        }
                    }

                    // Dừng sau khi tìm thấy
                    System.Windows.Application.Current.Dispatcher.Invoke(StopAuto);
                }
            }
        }

        private void PerformClick(int x, int y)
        {
            SetCursorPos(x, y);
            mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, 0);
            mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, 0);
        }

        private void AppendLog(string msg)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                LogText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + LogText;
            });
        }
    }
}