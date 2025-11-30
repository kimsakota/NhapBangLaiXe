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
        private readonly IApiService _apiService; // [Thêm]
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
            IApiService apiService, // [Thêm]
            AutoViewModel autoViewModel)
        {
            _contentDialogService = contentDialogService;
            _dataService = dataService;
            _recordService = recordService;
            _ocrService = ocrService;
            _apiService = apiService; // [Thêm]
            _autoViewModel = autoViewModel;

            // [LƯU Ý] Folder Macros cũng nên được quản lý gọn gàng, nhưng tạm thời giữ nguyên
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

            // [YÊU CẦU 1] Lưu backup ngay khi mở hồ sơ để tránh mất điện/crash
            _dataService.BackupSingleProfile(SelectedProfile);

            var dialogControl = new ChiTietDialog { DataContext = SelectedProfile };
            var dialog = new ContentDialog
            {
                Title = $"Hồ sơ: {SelectedProfile.LicensePlate}", // Hiện biển số lên tiêu đề
                Content = dialogControl,
                PrimaryButtonText = "Lưu & Chuyển",
                CloseButtonText = "Đóng",
                DefaultButton = ContentDialogButton.Close,
            };

            // Hiển thị Dialog
            _contentDialogService.ShowAsync(dialog, CancellationToken.None);

            dialog.Closing += async (s, e) =>
            {
                if (e.Result == ContentDialogResult.Primary)
                {
                    // [MỚI - QUAN TRỌNG] Logic gửi hồ sơ đi (API Confirm) trước khi lưu local

                    // 1. Gửi API xác nhận nhập xong (/api/{role}/nhap)
                    // Sử dụng LicensePlate làm ID
                    if (!string.IsNullOrEmpty(SelectedProfile.LicensePlate))
                    {
                        // Gọi API (có thể await để đảm bảo server nhận được)
                        bool apiResult = await _apiService.ConfirmImportedAsync(SelectedProfile.LicensePlate);

                        if (apiResult)
                        {
                            Debug.WriteLine($"=> Đã gửi API thành công cho biển số: {SelectedProfile.LicensePlate}");
                        }
                        else
                        {
                            // Nếu API lỗi, vẫn cho lưu local nhưng log lỗi
                            Debug.WriteLine($"=> Gửi API THẤT BẠI cho biển số: {SelectedProfile.LicensePlate}");
                            // Có thể hiện thông báo nhỏ nếu cần thiết, nhưng để workflow nhanh thì thường bỏ qua
                        }
                    }

                    // 2. Chuyển vào mục Đã lưu (Local File)
                    _dataService.MoveToSaved(SelectedProfile);

                    // 3. Cập nhật UI (Xóa khỏi danh sách chờ)
                    Profiles.Remove(SelectedProfile);
                    Count--;
                }
                else
                {
                    SelectedProfile = null;
                }
            };
        }

        // ... [GIỮ NGUYÊN CÁC HÀM RecordAsync và PlayAsync] ...
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
                            if (parallelZones.Any(z => z.ScanTimeout == 0)) maxTimeout = 0;
                            else maxTimeout = parallelZones.Max(z => z.ScanTimeout);
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
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Record chính đã xong -> Dừng quét song song.");
                                break;
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

                            foreach (var targetZone in sequentialZones)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"Kiểm tra sau chạy: {targetZone.Keyword}...");
                                await Task.Delay(500);

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
                                    break;
                                }
                                else
                                {
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