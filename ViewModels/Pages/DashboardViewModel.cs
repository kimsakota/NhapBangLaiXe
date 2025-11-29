using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Diagnostics;
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
        private readonly IContentDialogService _contentDialogService;
        private readonly IDataService _dataService;
        private readonly IRecordService _recordService;

        private readonly MinitouchHelper _minitouch; 

        private CancellationTokenSource? _cts;

        [ObservableProperty]
        private ObservableCollection<DriverProfile> _profiles = new();

        [ObservableProperty]
        private DriverProfile? _selectedProfile;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        private bool _isRecording = false;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        private bool _isPlaying = false;

        public bool IsBusy => IsRecording || IsPlaying;

        [ObservableProperty]
        private int? _count = 0;

        // [MỚI] Biến lưu số lần lặp (Mặc định 1 lần)
        [ObservableProperty]
        private int _loopCount = 100;

        public DashboardViewModel(
            IContentDialogService contentDialogService,
            IDataService dataService,
            IRecordService recordService,
            MinitouchHelper minitouch)
        {
            _contentDialogService = contentDialogService;
            _dataService = dataService;
            _recordService = recordService;
            _minitouch = minitouch;
            Task.Run (() => _minitouch.Start());
        }

        public Task OnNavigatedToAsync()
        {
            LoadData();
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
                else
                {
                    SelectedProfile = null;
                }
            };
        }

        [RelayCommand]
        private void RecordAsync()
        {
            if (IsPlaying)
            {
                MessageBox.Show("Đang chạy auto, vui lòng dừng chạy trước khi ghi âm.", "Thông báo");
                return;
            }

            IsRecording = !IsRecording;

            if (IsRecording)
            {
                _recordService.StartRecording();
            }
            else
            {
                _recordService.StopRecording();
                MessageBox.Show("Đã lưu thao tác thành công!", "Thông báo");
            }
        }

        // [CẬP NHẬT] Hàm chạy Record có hỗ trợ vòng lặp
        [RelayCommand]
        private async Task PlayAsync()
        {
            #region hihi
            if (IsRecording)
            {
                MessageBox.Show("Đang trong chế độ Ghi âm. Vui lòng dừng ghi trước.", "Cảnh báo");
                return;
            }

            // Nếu chưa chạy -> Bắt đầu chạy
            if (!IsPlaying)
            {
                IsPlaying = true;
                _cts = new CancellationTokenSource();
                var token = _cts.Token;

                try
                {
                    int currentRun = 0;

                    // Vòng lặp thực thi
                    while (!token.IsCancellationRequested)
                    {
                        // Kiểm tra số lần chạy:
                        // Nếu LoopCount > 0 (có giới hạn) và đã chạy đủ -> Dừng
                        if (LoopCount > 0 && currentRun >= LoopCount)
                        {
                            break;
                        }

                        // Gọi Service chạy 1 lượt
                        await _recordService.PlayRecordingAsync(token);

                        currentRun++;

                        // Nghỉ một chút giữa các lần lặp (tránh máy bị đơ)
                        // Chỉ nghỉ nếu còn chạy tiếp
                        if (LoopCount == 0 || currentRun < LoopCount)
                        {
                            await Task.Delay(200, token);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Bắt lỗi khi bấm Dừng hoặc Ctrl + S -> Không làm gì cả
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xảy ra: {ex.Message}", "Lỗi");
                }
                finally
                {
                    IsPlaying = false;
                    if (_cts != null)
                    {
                        _cts.Dispose();
                        _cts = null;
                    }
                }
            }
            // Nếu đang chạy -> Bấm lần nữa để Dừng
            else
            {
                _cts?.Cancel();
            }
            #endregion hihi
        }

        [RelayCommand]
        private async Task TestAsync()
        {
            _minitouch.Tap(800, 450);
            await Task.Delay(500);
            _minitouch.Swipe(100, 500, 100, 200);
        }
    }
}