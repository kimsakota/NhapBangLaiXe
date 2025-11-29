using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ToolVip.Services;
using Application = System.Windows.Application;

namespace ToolVip.ViewModels.Pages
{
    public partial class AutoRunViewModel : ObservableObject
    {
        private readonly IRecordService _recordService;
        private readonly IOcrService _ocrService;
        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private bool _isRunning = false;

        [ObservableProperty]
        private bool _isWaitingForSignal = false; // Trạng thái đang đợi từ khóa

        [ObservableProperty]
        private string _logText = "";

        [ObservableProperty]
        private string _statusText = "Sẵn sàng"; // Hiển thị trạng thái hiện tại (Đợi/Chạy)

        [ObservableProperty]
        private string _detectedText = "Chưa quét...";

        public AutoRunViewModel(IRecordService recordService, IOcrService ocrService)
        {
            _recordService = recordService;
            _ocrService = ocrService;
        }

        [RelayCommand]
        private async Task ToggleRunAsync()
        {
            // Nếu đang chạy -> Dừng
            if (IsRunning)
            {
                StopAuto();
                return;
            }

            // --- KHỞI TẠO ---
            IsRunning = true;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            var config = AutoConfigViewModel.CurrentConfig;

            // Khởi tạo OCR
            if (config.IsOcrEnabled)
            {
                _ocrService.Init(config.Language);
                AppendLog($"[System] Đã khởi động OCR ({config.Language}). Đang tìm từ khóa: '{config.StopKeyword}'...");
            }
            else
            {
                // Nếu không bật OCR thì chạy thẳng Macro luôn
                AppendLog("[System] OCR đang tắt -> Chạy Macro ngay lập tức.");
            }

            try
            {
                // --- GIAI ĐOẠN 1: QUÉT TÌM TỪ KHÓA (TRIGGER) ---
                if (config.IsOcrEnabled && !string.IsNullOrWhiteSpace(config.StopKeyword))
                {
                    IsWaitingForSignal = true;
                    StatusText = $"Đang tìm '{config.StopKeyword}'...";

                    while (!token.IsCancellationRequested)
                    {
                        string text = "";
                        try
                        {
                            // 1. Quét màn hình
                            text = _ocrService.GetTextFromScreen();

                            // Cập nhật UI
                            string cleanText = text.Replace("\n", " ").Trim();
                            Application.Current.Dispatcher.Invoke(() => DetectedText = cleanText);

                            // 2. Kiểm tra từ khóa
                            if (!string.IsNullOrEmpty(text) &&
                                text.Contains(config.StopKeyword, StringComparison.OrdinalIgnoreCase))
                            {
                                AppendLog($"[OCR] Đã phát hiện tín hiệu '{config.StopKeyword}'! => Kích hoạt Macro.");
                                break; // THOÁT VÒNG LẶP CHỜ -> SANG GIAI ĐOẠN 2
                            }
                        }
                        catch (Exception ex)
                        {
                            // Lỗi OCR không nên làm sập app, chỉ log nhẹ
                            // AppendLog($"[OCR Error] {ex.Message}");
                        }

                        // Chưa thấy -> Chờ quét tiếp
                        await Task.Delay(config.OcrInterval, token);
                    }
                }

                // --- GIAI ĐOẠN 2: CHẠY MACRO ---
                // (Chỉ xuống được đây khi đã thấy từ khóa hoặc OCR tắt)

                if (!token.IsCancellationRequested)
                {
                    IsWaitingForSignal = false;
                    StatusText = "Đang chạy Macro...";
                    AppendLog("[Macro] Bắt đầu thực thi kịch bản...");

                    // Vòng lặp chạy Macro
                    while (!token.IsCancellationRequested)
                    {
                        await _recordService.PlayRecordingAsync(token);

                        // Nghỉ một chút giữa các lần lặp lại kịch bản (tránh spam CPU)
                        await Task.Delay(500, token);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                AppendLog("[System] Đã dừng thủ công.");
            }
            catch (Exception ex)
            {
                AppendLog($"[Error] {ex.Message}");
                System.Windows.MessageBox.Show(ex.Message, "Lỗi vận hành");
            }
            finally
            {
                // Reset trạng thái
                StopAuto();
            }
        }

        private void StopAuto()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            IsRunning = false;
            IsWaitingForSignal = false;
            StatusText = "Sẵn sàng";
        }

        private void AppendLog(string msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                LogText = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + LogText;
            });
        }
    }
}