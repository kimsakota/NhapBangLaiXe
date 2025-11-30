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
        private readonly IApiService _apiService;
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
            IApiService apiService,
            AutoViewModel autoViewModel)
        {
            _contentDialogService = contentDialogService;
            _dataService = dataService;
            _recordService = recordService;
            _ocrService = ocrService;
            _apiService = apiService;
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

            _dataService.BackupSingleProfile(SelectedProfile);

            var dialogControl = new ChiTietDialog { DataContext = SelectedProfile };
            var dialog = new ContentDialog
            {
                Title = $"Hồ sơ: {SelectedProfile.LicensePlate}",
                Content = dialogControl,
                PrimaryButtonText = "Lưu & Chuyển",
                CloseButtonText = "Đóng",
                DefaultButton = ContentDialogButton.Close,
            };

            _contentDialogService.ShowAsync(dialog, CancellationToken.None);

            dialog.Closing += async (s, e) =>
            {
                if (e.Result == ContentDialogResult.Primary)
                {
                    if (!string.IsNullOrEmpty(SelectedProfile.LicensePlate))
                    {
                        bool apiResult = await _apiService.ConfirmImportedAsync(SelectedProfile.LicensePlate);

                        if (apiResult)
                        {
                            Debug.WriteLine($"=> Đã gửi API thành công cho biển số: {SelectedProfile.LicensePlate}");
                        }
                        else
                        {
                            Debug.WriteLine($"=> Gửi API THẤT BẠI cho biển số: {SelectedProfile.LicensePlate}");
                        }
                    }

                    _dataService.MoveToSaved(SelectedProfile);
                    Profiles.Remove(SelectedProfile);
                    Count--;
                }
                else
                {
                    SelectedProfile = null;
                }
            };
        }

        [RelayCommand]
        private void OpenRecord()
        {
            if (IsBusy) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Macro Files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Chọn file Macro để làm Record Chính"
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    var filePath = dialog.FileName;
                    var json = File.ReadAllText(filePath);
                    var events = JsonSerializer.Deserialize<List<MacroEvent>>(json);

                    if (events != null && events.Count > 0)
                    {
                        _dashboardMacroEvents = events;
                        _recordService.SaveRecording(_dashboardMacroEvents, _recordPath);
                        MessageBox.Show($"Đã nạp file record mới thành công!\n- Nguồn: {Path.GetFileName(filePath)}\n- Số bước: {events.Count}\n- Đã lưu đè vào MainLoop.", "Thành công");
                    }
                    else
                    {
                        MessageBox.Show("File rỗng hoặc sai định dạng.", "Lỗi");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Lỗi đọc file: " + ex.Message, "Lỗi");
                }
            }
        }

        [RelayCommand]
        private void RecordAsync()
        {
            if (IsPlaying) { MessageBox.Show("Đang chạy auto...", "Thông báo"); return; }

            if (IsRecording)
            {
                StopRecordingInternal();
                return;
            }

            IsRecording = true;
            _recordService.StartRecording();

            _ = Task.Run(async () =>
            {
                while (IsRecording)
                {
                    bool isCtrl = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;
                    bool isS = (GetAsyncKeyState(VK_S) & 0x8000) != 0;

                    if (isCtrl && isS)
                    {
                        System.Windows.Application.Current.Dispatcher.Invoke(StopRecordingInternal);
                        break;
                    }
                    await Task.Delay(50);
                }
            });
        }

        private void StopRecordingInternal()
        {
            if (!IsRecording) return;
            IsRecording = false;

            var recordedEvents = _recordService.StopRecordingAndGet();
            _dashboardMacroEvents = new List<MacroEvent>(recordedEvents);

            if (_autoViewModel.ScanZones.Count > 0)
            {
                _recordService.SaveRecording(recordedEvents, _recordPath);
                MessageBox.Show($"Đã lưu Record Chính ({recordedEvents.Count} bước).", "Đã lưu");
            }
            else MessageBox.Show("Vui lòng tạo vùng quét trước (sang tab OCR để tạo).", "Lưu ý");
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
                    // [MỚI] Vòng lặp chính - Lặp lại cho đến khi bấm Dừng hoặc hết danh sách
                    int currentLoop = 0;
                    while (!token.IsCancellationRequested && currentLoop < LoopCount)
                    {
                        // [QUAN TRỌNG] Kiểm tra danh sách có còn không
                        int remainingCount = 0;
                        System.Windows.Application.Current.Dispatcher.Invoke(() => remainingCount = Profiles.Count);

                        if (remainingCount == 0)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                _autoViewModel.LogText = "========== HẾT DANH SÁCH - DỪNG TỰ ĐỘNG ==========");
                            break;
                        }

                        currentLoop++;
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            _autoViewModel.LogText = $"========== VÒNG LẶP {currentLoop}/{LoopCount} (Còn {remainingCount} hồ sơ) ==========");

                        await Task.Run(async () =>
                        {
                            var allZones = _autoViewModel.ScanZones.ToList();
                            var parallelZones = allZones.Where(z => z.RunStrategy == 0).ToList();
                            var sequentialZones = allZones.Where(z => z.RunStrategy == 1).ToList();
                            bool runMainAsync = parallelZones.Any();

                            Debug.WriteLine($"--- BẮT ĐẦU AUTO ---");
                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "--- BẮT ĐẦU AUTO ---");

                            using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                            Task playTask;

                            if (runMainAsync)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Chạy Record chính (Song song)...");
                                playTask = _recordService.PlayRecordingAsync(_dashboardMacroEvents, loopCts.Token);
                            }
                            else
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Chạy Record chính (Tuần tự)...");
                                await _recordService.PlayRecordingAsync(_dashboardMacroEvents, token);
                                playTask = Task.CompletedTask;
                            }

                            var stopWatch = Stopwatch.StartNew();
                            bool recordStoppedByOcr = false;
                            int maxTimeout = 0;
                            if (parallelZones.Any())
                            {
                                maxTimeout = parallelZones.Max(z => z.ScanTimeout);
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"Tổng thời gian quét: {maxTimeout} giây");
                            }

                            while (true)
                            {
                                if (token.IsCancellationRequested) break;
                                if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 && (GetAsyncKeyState(VK_S) & 0x8000) != 0)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Người dùng bấm Ctrl+S");
                                    loopCts.Cancel(); _cts.Cancel(); break;
                                }

                                if (maxTimeout > 0 && stopWatch.Elapsed.TotalSeconds > maxTimeout)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Hết thời gian quét (Timeout).");
                                    break;
                                }

                                if (runMainAsync && playTask.IsCompleted)
                                {
                                    if (maxTimeout == 0)
                                    {
                                        System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Record chính đã xong -> Dừng quét song song.");
                                        break;
                                    }
                                }

                                if (!parallelZones.Any()) break;

                                bool foundAnyInLoop = false;
                                foreach (var targetZone in parallelZones)
                                {
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
                                        foundAnyInLoop = true;
                                        recordStoppedByOcr = true;
                                        System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> TÌM THẤY: {targetZone.Keyword}!");

                                        if (!playTask.IsCompleted)
                                        {
                                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> Dừng Record Chính...");
                                            loopCts.Cancel();
                                            try { await playTask; } catch { }
                                        }

                                        if (targetZone.FoundActions.Count > 0)
                                        {
                                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> Chạy Action của '{targetZone.Keyword}'...");
                                            await _recordService.PlayRecordingAsync(targetZone.FoundActions, token);
                                        }

                                        break;
                                    }
                                }

                                if (foundAnyInLoop) break;

                                await Task.Delay(200);
                            }

                            if (!recordStoppedByOcr && !token.IsCancellationRequested)
                            {
                                if (!playTask.IsCompleted) { try { await playTask; } catch { } }

                                if (parallelZones.Any())
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> Hết thời gian (Timeout): Kiểm tra hành động Not Found...");
                                    foreach (var pZone in parallelZones)
                                    {
                                        if (pZone.NotFoundActions.Count > 0)
                                        {
                                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> Chạy NotFound Action của vùng: {pZone.Keyword}");
                                            await _recordService.PlayRecordingAsync(pZone.NotFoundActions, token);
                                        }
                                    }
                                }

                                // [SỬA] Xử lý vùng quét TUẦN TỰ (Sau khi Record chính xong)
                                foreach (var targetZone in sequentialZones)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"Bắt đầu quét sau chạy: {targetZone.Keyword}...");

                                    int x = Math.Min(targetZone.X1, targetZone.X2);
                                    int y = Math.Min(targetZone.Y1, targetZone.Y2);
                                    int w = Math.Abs(targetZone.X1 - targetZone.X2);
                                    int h = Math.Abs(targetZone.Y1 - targetZone.Y2);

                                    // Xác định thời gian quét tối đa
                                    int scanTimeoutSeconds = targetZone.ScanTimeout;
                                    var scanStopwatch = Stopwatch.StartNew();
                                    bool foundInSequential = false;

                                    // Quét liên tục trong khoảng thời gian cho phép
                                    while (true)
                                    {
                                        if (token.IsCancellationRequested) break;

                                        // Kiểm tra Ctrl+S
                                        if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 && (GetAsyncKeyState(VK_S) & 0x8000) != 0)
                                        {
                                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Người dùng bấm Ctrl+S");
                                            loopCts.Cancel(); _cts.Cancel(); break;
                                        }

                                        // Kiểm tra hết thời gian quét
                                        if (scanTimeoutSeconds > 0 && scanStopwatch.Elapsed.TotalSeconds > scanTimeoutSeconds)
                                        {
                                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"Hết thời gian quét cho '{targetZone.Keyword}' ({scanTimeoutSeconds}s)");
                                            break;
                                        }

                                        // Thực hiện OCR
                                        string scannedText = "";
                                        if (w > 0 && h > 0) scannedText = _ocrService.GetTextFromRegion(x, y, w, h);

                                        bool isFound = !string.IsNullOrEmpty(scannedText) &&
                                                       scannedText.Contains(targetZone.Keyword, StringComparison.OrdinalIgnoreCase);

                                        if (isFound)
                                        {
                                            foundInSequential = true;
                                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> TÌM THẤY (Sau chạy): {targetZone.Keyword}!");

                                            if (targetZone.FoundActions.Count > 0)
                                            {
                                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> Chạy Found Action của '{targetZone.Keyword}'...");
                                                await _recordService.PlayRecordingAsync(targetZone.FoundActions, token);
                                            }
                                            break; // Tìm thấy rồi thì dừng quét vùng này
                                        }

                                        // Chưa tìm thấy -> Chờ rồi quét lại
                                        await Task.Delay(200, token);
                                    }

                                    // Nếu hết thời gian quét mà KHÔNG tìm thấy -> Chạy NotFound
                                    if (!foundInSequential && !token.IsCancellationRequested)
                                    {
                                        if (targetZone.NotFoundActions.Count > 0)
                                        {
                                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"=> KHÔNG TÌM THẤY '{targetZone.Keyword}' -> Chạy NotFound Action");
                                            await _recordService.PlayRecordingAsync(targetZone.NotFoundActions, token);
                                        }
                                    }

                                    scanStopwatch.Stop();
                                }
                            }

                            if (!token.IsCancellationRequested)
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "--- HOÀN THÀNH ---");

                        }, token);

                        // [MỚI] Kiểm tra có tiếp tục vòng lặp không
                        if (token.IsCancellationRequested)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                _autoViewModel.LogText = $"Đã dừng sau {currentLoop} vòng lặp.");
                            break;
                        }

                        // Delay ngắn giữa các vòng lặp (tùy chọn)
                        await Task.Delay(500, token);
                    }

                    // Thông báo kết thúc tất cả vòng lặp
                    if (!token.IsCancellationRequested)
                    {
                        int finalCount = 0;
                        System.Windows.Application.Current.Dispatcher.Invoke(() => finalCount = Profiles.Count);

                        if (finalCount == 0)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                _autoViewModel.LogText = $"========== ĐÃ XỬ LÝ HẾT {currentLoop} HỒ SƠ ==========");
                        }
                        else
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                                _autoViewModel.LogText = $"========== HOÀN THÀNH {currentLoop} VÒNG LẶP (Còn {finalCount} hồ sơ) ==========");
                        }
                    }
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