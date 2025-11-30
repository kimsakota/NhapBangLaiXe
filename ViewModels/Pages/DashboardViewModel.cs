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
                else MessageBox.Show("Vui lòng tạo vùng quét trước.", "Lỗi");
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
                        var targetZone = _autoViewModel.ScanZones[0];
                        bool isRunParallel = targetZone.RunStrategy == 0; // 0: Song song, 1: Sau khi chạy
                        int timeoutSeconds = targetZone.ScanTimeout;      // 0: Theo Record chính / Vô hạn

                        Debug.WriteLine($"--- BẮT ĐẦU (Strategy: {(isRunParallel ? "Song Song" : "Sau khi chạy")}, Timeout: {timeoutSeconds}s) ---");
                        System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "--- BẮT ĐẦU AUTO ---");

                        // Tạo CancellationTokenSource riêng để quản lý Record Chính
                        // Nếu tìm thấy OCR -> Cancel thằng này để dừng Record Chính
                        using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(token);

                        Task playTask;

                        // --- GIAI ĐOẠN 1: KHỞI ĐỘNG RECORD CHÍNH ---
                        if (isRunParallel)
                        {
                            // SONG SONG: Chạy Record ngay lập tức, không chờ (Assign Task)
                            playTask = _recordService.PlayRecordingAsync(_dashboardMacroEvents, loopCts.Token);
                        }
                        else
                        {
                            // TUẦN TỰ: Chạy Record xong mới đi tiếp
                            // Dùng 'token' gốc vì loopCts dùng để cancel khi OCR thấy (nhưng ở đây OCR chưa chạy)
                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Đang chạy Record chính...");
                            await _recordService.PlayRecordingAsync(_dashboardMacroEvents, token);
                            playTask = Task.CompletedTask; // Đánh dấu là đã xong

                            // Nếu người dùng bấm Stop trong lúc chạy Record
                            if (token.IsCancellationRequested) return;
                        }

                        // --- GIAI ĐOẠN 2: QUÉT OCR ---
                        bool foundKeyword = false;
                        var stopWatch = Stopwatch.StartNew();

                        while (true)
                        {
                            // 1. Kiểm tra dừng bởi người dùng (Nút Stop hoặc Ctrl+S)
                            if (token.IsCancellationRequested) break;
                            if ((GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0 && (GetAsyncKeyState(VK_S) & 0x8000) != 0)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Người dùng bấm Ctrl+S");
                                loopCts.Cancel(); _cts.Cancel(); break;
                            }

                            // 2. Kiểm tra Thời gian (Timeout)
                            // Nếu Timeout > 0: Quét đủ thời gian thì dừng
                            if (timeoutSeconds > 0 && stopWatch.Elapsed.TotalSeconds > timeoutSeconds)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Hết thời gian quét (Timeout).");
                                break;
                            }

                            // 3. Logic thoát vòng lặp tùy theo chế độ
                            if (isRunParallel)
                            {
                                // Song song + Timeout = 0 (Theo record): Nếu Record chính xong thì dừng quét
                                if (timeoutSeconds == 0 && playTask.IsCompleted)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "Record chính đã xong -> Dừng quét.");
                                    break;
                                }
                            }
                            else
                            {
                                // Tuần tự + Timeout = 0: (Mặc định quét 1 lần hoặc khoảng ngắn rồi nghỉ để tránh loop vô hạn)
                                if (timeoutSeconds == 0 && stopWatch.Elapsed.TotalSeconds > 2) break;
                            }

                            // 4. THỰC HIỆN OCR
                            int x = Math.Min(targetZone.X1, targetZone.X2);
                            int y = Math.Min(targetZone.Y1, targetZone.Y2);
                            int w = Math.Abs(targetZone.X1 - targetZone.X2);
                            int h = Math.Abs(targetZone.Y1 - targetZone.Y2);

                            string scannedText = "";
                            if (w > 0 && h > 0) scannedText = _ocrService.GetTextFromRegion(x, y, w, h);

                            System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = $"OCR đang tìm '{targetZone.Keyword}': {scannedText}");

                            bool isFound = !string.IsNullOrEmpty(scannedText) &&
                                           scannedText.Contains(targetZone.Keyword, StringComparison.OrdinalIgnoreCase);

                            if (isFound)
                            {
                                foundKeyword = true;
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> TÌM THẤY TỪ KHÓA!");

                                // --- LOGIC QUAN TRỌNG THEO YÊU CẦU ---
                                // "lúc tìm thấy từ khóa thì, play sẽ dừng hẳn."
                                if (!playTask.IsCompleted)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> Dừng Record Chính...");
                                    loopCts.Cancel(); // Hủy lệnh chạy Record chính
                                    try
                                    {
                                        await playTask; // Đợi nó dừng hẳn
                                    }
                                    catch (OperationCanceledException) { }
                                    catch (Exception) { }
                                }

                                // "Và record của tìm thấy sẽ chạy cho xong."
                                if (targetZone.FoundActions.Count > 0)
                                {
                                    System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> Chạy Record 'Tìm Thấy'...");
                                    await _recordService.PlayRecordingAsync(targetZone.FoundActions, token);
                                }
                                else System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> (Không có hành động 'Tìm Thấy' được cài đặt)");

                                break; // Thoát vòng lặp quét
                            }

                            await Task.Delay(200); // Nghỉ một chút giữa các lần quét
                        }

                        // --- GIAI ĐOẠN 3: XỬ LÝ KHI KHÔNG TÌM THẤY ---
                        if (!foundKeyword && !token.IsCancellationRequested)
                        {
                            // Nếu là Song song và Record chính vẫn đang chạy (do hết Timeout mà chưa xong), có muốn dừng nó không?
                            // Thường là không, cứ để nó chạy nốt. Nhưng nếu muốn chắc chắn dừng thì uncomment dòng dưới:
                            // if (!playTask.IsCompleted) { loopCts.Cancel(); try { await playTask; } catch {} }

                            if (targetZone.NotFoundActions.Count > 0)
                            {
                                System.Windows.Application.Current.Dispatcher.Invoke(() => _autoViewModel.LogText = "=> Chạy Record 'Không Thấy'...");
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