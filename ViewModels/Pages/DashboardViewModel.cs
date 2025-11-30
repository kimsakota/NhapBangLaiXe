using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json; // [MỚI] Để Deserialize
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
        private readonly string _recordPath; // [MỚI] Đường dẫn file record chính

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

            // [MỚI] Định nghĩa đường dẫn file record chính
            _recordPath = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Macros", "Record_MainLoop.json");

            // [MỚI] Thử load record cũ nếu có
            LoadRecordedEvents();
        }

        // [MỚI] Hàm load record từ file
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
            // Load lại lần nữa để chắc chắn (nếu file bị sửa bên ngoài)
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

        // ... (Giữ nguyên phần SelectedProfileChanged) ...
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
                    // Lưu file dùng đường dẫn chung đã định nghĩa
                    _recordService.SaveRecording(recordedEvents, _recordPath);
                    MessageBox.Show($"Đã lưu Record Chính ({recordedEvents.Count} bước).", "Đã lưu");
                }
                else MessageBox.Show("Vui lòng tạo vùng quét trước.", "Lỗi");
            }
        }

        [RelayCommand]
        private async Task PlayAsync()
        {
            if (IsRecording) { MessageBox.Show("Đang ghi âm.", "Cảnh báo"); return; }

            // [MỚI] Kiểm tra kỹ hơn: Nếu list rỗng thì thử load lại từ file lần cuối
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
                        var targetZone = _autoViewModel.ScanZones[0];
                        bool isRunParallel = targetZone.RunStrategy == 0;
                        int timeoutSeconds = targetZone.ScanTimeout;

                        Debug.WriteLine($"--- BẮT ĐẦU (Strategy: {(isRunParallel ? "Song Song" : "Sau khi chạy")}, Timeout: {timeoutSeconds}s) ---");

                        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(token);

                        // Bước 1: Chạy Record Chính
                        var playTask = _recordService.PlayRecordingAsync(_dashboardMacroEvents, loopCts.Token);

                        if (!isRunParallel)
                        {
                            try { await playTask; } catch { }
                            if (token.IsCancellationRequested) return;
                        }

                        // Bước 2: Bắt đầu vòng lặp quét OCR
                        bool foundKeyword = false;
                        var stopWatch = Stopwatch.StartNew();

                        while (true)
                        {
                            if (token.IsCancellationRequested) break;
                            if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 && (GetAsyncKeyState(VK_S) & 0x8000) != 0)
                            {
                                Debug.WriteLine("Ctrl+S -> STOP");
                                loopCts.Cancel(); _cts.Cancel(); break;
                            }

                            if (timeoutSeconds > 0 && stopWatch.Elapsed.TotalSeconds > timeoutSeconds)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Hết thời gian quét (Timeout).");
                                break;
                            }

                            if (isRunParallel && playTask.IsCompleted && timeoutSeconds == 0) break;

                            // D. Thực hiện OCR
                            int x = Math.Min(targetZone.X1, targetZone.X2);
                            int y = Math.Min(targetZone.Y1, targetZone.Y2);
                            int w = Math.Abs(targetZone.X1 - targetZone.X2);
                            int h = Math.Abs(targetZone.Y1 - targetZone.Y2);

                            string scannedText = "";
                            if (w > 0 && h > 0) scannedText = _ocrService.GetTextFromRegion(x, y, w, h);

                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"OCR: {scannedText}");

                            bool isFound = !string.IsNullOrEmpty(scannedText) &&
                                           scannedText.Contains(targetZone.Keyword, StringComparison.OrdinalIgnoreCase);

                            if (isFound)
                            {
                                Debug.WriteLine("=> TÌM THẤY TỪ KHÓA!");
                                foundKeyword = true;

                                if (!playTask.IsCompleted)
                                {
                                    loopCts.Cancel();
                                    try { await playTask; } catch { }
                                }

                                if (targetZone.FoundActions.Count > 0)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> Chạy FoundActions...");
                                    await _recordService.PlayRecordingAsync(targetZone.FoundActions, token);
                                }
                                else System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> Found (No Actions)");

                                break;
                            }

                            await Task.Delay(200);
                        }

                        if (!foundKeyword && !token.IsCancellationRequested)
                        {
                            if (!playTask.IsCompleted)
                            {
                                loopCts.Cancel();
                                try { await playTask; } catch { }
                            }

                            if (targetZone.NotFoundActions.Count > 0)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> Chạy NotFoundActions...");
                                await _recordService.PlayRecordingAsync(targetZone.NotFoundActions, token);
                            }
                            else
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Kết thúc: Không tìm thấy.");
                            }
                        }

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