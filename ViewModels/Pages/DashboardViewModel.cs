using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ToolVip.Helpers;
using ToolVip.Models;
using ToolVip.Services;
using ToolVip.Views.UseControls;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ToolVip.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, INavigationAware
    {
        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);
        private const int VK_CONTROL = 0x11;
        private const int VK_S = 0x53;

        private readonly IContentDialogService _contentDialogService;
        private readonly IDataService _dataService;
        private readonly IRecordService _recordService;
        private readonly IOcrService _ocrService;
        private readonly AutoViewModel _autoViewModel;

        private CancellationTokenSource? _cts;
        private List<MacroEvent> _dashboardMacroEvents = new();
        private readonly string _recordPath;

        [ObservableProperty] private ObservableCollection<DriverProfile> _profiles = new();
        [ObservableProperty] private DriverProfile? _selectedProfile;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsBusy))] private bool _isRecording = false;
        [ObservableProperty][NotifyPropertyChangedFor(nameof(IsBusy))] private bool _isPlaying = false;

        public bool IsBusy => IsRecording || IsPlaying;

        [ObservableProperty] private int? _count = 0;
        [ObservableProperty] private int _loopCount = 100;

        public DashboardViewModel(
            IContentDialogService contentDialogService,
            IDataService dataService,
            IRecordService recordService,
            IOcrService ocrService,
            AutoViewModel autoViewModel)
        {
            _contentDialogService = contentDialogService;
            _dataService = dataService;
            _recordService = recordService;
            _ocrService = ocrService;
            _autoViewModel = autoViewModel;

            _recordPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Macros", "Record_MainLoop.json");

            LoadRecordedEvents();
        }

        private void LoadRecordedEvents()
        {
            if (File.Exists(_recordPath))
            {
                try
                {
                    var json = File.ReadAllText(_recordPath);
                    var events = JsonSerializer.Deserialize<List<MacroEvent>>(json);
                    if (events != null && events.Count > 0)
                    {
                        _dashboardMacroEvents = events;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Lỗi load record cũ: {ex.Message}");
                }
            }
        }

        public Task OnNavigatedToAsync()
        {
            LoadData();
            LoadRecordedEvents();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

        private void LoadData()
        {
            var data = _dataService.LoadPendingData();
            Profiles = new ObservableCollection<DriverProfile>(data);
            Count = Profiles.Count;
        }

        partial void OnSelectedProfileChanged(DriverProfile? value)
        {
            if (SelectedProfile == null) return;
            var dialogControl = new ChiTietDialog { DataContext = SelectedProfile };
            var dialog = new ContentDialog
            {
                Title = "Chi tiết hồ sơ",
                Content = dialogControl,
                PrimaryButtonText = "Lưu & Chuyển",
                CloseButtonText = "Đóng",
                DefaultButton = ContentDialogButton.Close,
            };
            _contentDialogService.ShowAsync(dialog, CancellationToken.None);
            dialog.Closing += (s, e) =>
            {
                if (e.Result == ContentDialogResult.Primary)
                {
                    _dataService.MoveToSaved(SelectedProfile);
                    Profiles.Remove(SelectedProfile);
                    Count--;
                }
                else SelectedProfile = null;
            };
        }

        [RelayCommand]
        private void RecordAsync()
        {
            if (IsPlaying) { MessageBox.Show("Đang chạy auto...", "Thông báo"); return; }
            IsRecording = !IsRecording;

            if (IsRecording) _recordService.StartRecording();
            else
            {
                var recordedEvents = _recordService.StopRecordingAndGet();
                _dashboardMacroEvents = new List<MacroEvent>(recordedEvents);

                if (_autoViewModel.ScanZones.Count > 0)
                {
                    _recordService.SaveRecording(recordedEvents, _recordPath);
                    MessageBox.Show($"Đã lưu Record Chính ({recordedEvents.Count} bước).", "Đã lưu");
                }
                else MessageBox.Show("Vui lòng tạo vùng quét trước (sang tab OCR để tạo).", "Lưu ý");
            }
        }

        [RelayCommand]
        private async Task PlayAsync()
        {
            if (IsRecording) { MessageBox.Show("Đang ghi âm.", "Cảnh báo"); return; }

            if (_dashboardMacroEvents.Count == 0) LoadRecordedEvents();

            if (_dashboardMacroEvents.Count == 0)
            {
                MessageBox.Show("Chưa có Record Chính (quay bằng nút ở Dashboard). Vui lòng quay trước.", "Lỗi");
                return;
            }

            // [FIX] Kiểm tra Count thay vì lấy phần tử [0] ngay
            if (_autoViewModel.ScanZones.Count == 0) { MessageBox.Show("Chưa có vùng quét.", "Lỗi"); return; }

            var initResult = _ocrService.Init("vie");
            if (!initResult.Success) { MessageBox.Show(initResult.Message, "Lỗi OCR"); return; }

            if (!IsPlaying)
            {
                IsPlaying = true;
                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                try
                {
                    await Task.Run(async () =>
                    {
                        // [FIX] Lấy danh sách tất cả các vùng quét
                        var allZones = _autoViewModel.ScanZones.ToList();

                        // Phân loại vùng quét theo chiến thuật
                        var parallelZones = allZones.Where(z => z.RunStrategy == 0).ToList(); // Song song
                        var sequentialZones = allZones.Where(z => z.RunStrategy == 1).ToList(); // Tuần tự (Sau khi xong)

                        // Nếu có bất kỳ vùng nào là Song Song -> Chạy Record chính dạng Async (không chờ)
                        bool runMainAsync = parallelZones.Any();

                        Debug.WriteLine($"--- BẮT ĐẦU AUTO ---");
                        System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "--- BẮT ĐẦU AUTO ---");

                        // Tạo CancellationTokenSource riêng để quản lý Record Chính
                        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(token);

                        Task playTask;

                        // --- GIAI ĐOẠN 1: KHỞI ĐỘNG RECORD CHÍNH ---
                        if (runMainAsync)
                        {
                            // SONG SONG: Chạy Record ngay lập tức, không chờ
                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Chạy Record chính (Song song)...");
                            playTask = _recordService.PlayRecordingAsync(_dashboardMacroEvents, loopCts.Token);
                        }
                        else
                        {
                            // TUẦN TỰ: Chạy Record xong mới đi tiếp
                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Chạy Record chính (Tuần tự)...");
                            await _recordService.PlayRecordingAsync(_dashboardMacroEvents, token);
                            playTask = Task.CompletedTask; // Đánh dấu là đã xong
                        }

                        // --- GIAI ĐOẠN 2: QUÉT OCR SONG SONG (Cho các vùng Parallel) ---
                        var stopWatch = Stopwatch.StartNew();
                        bool recordStoppedByOcr = false;

                        // Timeout: Lấy cái lớn nhất hoặc 0 (vô hạn)
                        int maxTimeout = 0;
                        if (parallelZones.Any())
                        {
                            if (parallelZones.Any(z => z.ScanTimeout == 0)) maxTimeout = 0;
                            else maxTimeout = parallelZones.Max(z => z.ScanTimeout);
                        }

                        // Vòng lặp quét (Chỉ chạy khi có vùng song song hoặc đang đợi record chính chạy)
                        while (true)
                        {
                            // 1. Kiểm tra dừng bởi người dùng
                            if (token.IsCancellationRequested) break;
                            if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 && (GetAsyncKeyState(VK_S) & 0x8000) != 0)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Người dùng bấm Ctrl+S");
                                loopCts.Cancel(); _cts.Cancel(); break;
                            }

                            // 2. Kiểm tra Timeout
                            if (maxTimeout > 0 && stopWatch.Elapsed.TotalSeconds > maxTimeout)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Hết thời gian quét (Timeout).");
                                break;
                            }

                            // 3. Logic thoát: Nếu Record chính đã xong thì dừng quét song song
                            if (runMainAsync && playTask.IsCompleted)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Record chính đã xong -> Dừng quét song song.");
                                break;
                            }
                            // Nếu không có vùng song song nào thì thoát luôn để sang giai đoạn sau
                            if (!parallelZones.Any()) break;

                            // 4. [FIX] THỰC HIỆN OCR CHO TẤT CẢ CÁC VÙNG SONG SONG
                            bool foundAnyInLoop = false;
                            foreach (var targetZone in parallelZones)
                            {
                                int x = Math.Min(targetZone.X1, targetZone.X2);
                                int y = Math.Min(targetZone.Y1, targetZone.Y2);
                                int w = Math.Abs(targetZone.X1 - targetZone.X2);
                                int h = Math.Abs(targetZone.Y1 - targetZone.Y2);

                                string scannedText = "";
                                if (w > 0 && h > 0) scannedText = _ocrService.GetTextFromRegion(x, y, w, h);

                                // Debug log nếu cần
                                // System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"Quét '{targetZone.Keyword}': {scannedText}");

                                bool isFound = !string.IsNullOrEmpty(scannedText) &&
                                               scannedText.Contains(targetZone.Keyword, StringComparison.OrdinalIgnoreCase);

                                if (isFound)
                                {
                                    foundAnyInLoop = true;
                                    recordStoppedByOcr = true;
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> TÌM THẤY: {targetZone.Keyword}!");

                                    // Dừng Record Chính ngay lập tức
                                    if (!playTask.IsCompleted)
                                    {
                                        System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> Dừng Record Chính...");
                                        loopCts.Cancel();
                                        try { await playTask; } catch { }
                                    }

                                    // Chạy Record 'Tìm Thấy' của vùng đó
                                    if (targetZone.FoundActions.Count > 0)
                                    {
                                        System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> Chạy Action của '{targetZone.Keyword}'...");
                                        await _recordService.PlayRecordingAsync(targetZone.FoundActions, token);
                                    }

                                    break; // Thoát foreach
                                }
                            }

                            if (foundAnyInLoop) break; // Thoát while

                            await Task.Delay(200); // Nghỉ giữa các lần quét
                        }

                        // --- GIAI ĐOẠN 3: XỬ LÝ CÁC VÙNG TUẦN TỰ (Sequential) ---
                        // Chỉ chạy nếu chưa bị dừng bởi OCR Song Song
                        if (!recordStoppedByOcr && !token.IsCancellationRequested)
                        {
                            // Đảm bảo record chính đã xong
                            if (!playTask.IsCompleted) { try { await playTask; } catch { } }

                            foreach (var targetZone in sequentialZones)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"Kiểm tra sau chạy: {targetZone.Keyword}...");
                                await Task.Delay(500); // Đợi UI ổn định

                                int x = Math.Min(targetZone.X1, targetZone.X2);
                                int y = Math.Min(targetZone.Y1, targetZone.Y2);
                                int w = Math.Abs(targetZone.X1 - targetZone.X2);
                                int h = Math.Abs(targetZone.Y1 - targetZone.Y2);

                                string scannedText = "";
                                if (w > 0 && h > 0) scannedText = _ocrService.GetTextFromRegion(x, y, w, h);

                                bool isFound = !string.IsNullOrEmpty(scannedText) &&
                                               scannedText.Contains(targetZone.Keyword, StringComparison.OrdinalIgnoreCase);

                                if (isFound)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> TÌM THẤY (Sau chạy): {targetZone.Keyword}!");
                                    if (targetZone.FoundActions.Count > 0)
                                    {
                                        await _recordService.PlayRecordingAsync(targetZone.FoundActions, token);
                                    }
                                    // Thường tìm thấy 1 lỗi/kết quả là dừng xử lý tiếp
                                    break;
                                }
                                else
                                {
                                    // Nếu có action Not Found
                                    if (targetZone.NotFoundActions.Count > 0)
                                    {
                                        System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> KHÔNG THẤY (Sau chạy): {targetZone.Keyword} -> Chạy NotFound Action");
                                        await _recordService.PlayRecordingAsync(targetZone.NotFoundActions, token);
                                    }
                                }
                            }
                        }

                        if (!token.IsCancellationRequested)
                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "--- HOÀN THÀNH ---");

                    }, token);
                } 
                catch (Exception ex)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi"));
                }
                finally
                {
                    IsPlaying = false;
                    _cts?.Dispose();
                    _cts = null;
                }
            }
            else
            {
                _cts?.Cancel();
            }
        }
    }
}