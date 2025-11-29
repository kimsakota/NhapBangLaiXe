using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
        private readonly IRecordService _recordService; // Service xử lý ghi/phát

        private CancellationTokenSource? _cts; // Token để hủy tác vụ khi bấm Dừng

        [ObservableProperty]
        private ObservableCollection<DriverProfile> _profiles = new();

        [ObservableProperty]
        private DriverProfile? _selectedProfile;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        private bool _isRecording = false;

        // CẬP NHẬT: Thêm NotifyPropertyChangedFor
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsBusy))]
        private bool _isPlaying = false;

        public bool IsBusy => IsRecording || IsPlaying;

        [ObservableProperty]
        private int? _count = 0;

        // Inject thêm IRecordService vào Constructor
        public DashboardViewModel(
            IContentDialogService contentDialogService,
            IDataService dataService,
            IRecordService recordService)
        {
            _contentDialogService = contentDialogService;
            _dataService = dataService;
            _recordService = recordService;

            // XÓA HẾT DỮ LIỆU GIẢ LẬP CŨ Ở ĐÂY NẾU CẦN
        }

        // Mỗi khi quay lại trang Home, tự động load lại danh sách chờ
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

        // Xử lý khi chọn một dòng trong danh sách
        partial void OnSelectedProfileChanged(DriverProfile? value)
        {
            if (SelectedProfile == null) return;

            // Mở Dialog chi tiết
            var dialogControl = new ChiTietDialog { DataContext = SelectedProfile };
            var dialog = new ContentDialog
            {
                Title = "Chi tiết hồ sơ",
                Content = dialogControl,
                PrimaryButtonText = "Lưu & Chuyển", // Nút này sẽ lưu sang file Saved
                CloseButtonText = "Đóng",
                DefaultButton = ContentDialogButton.Close,
            };

            _contentDialogService.ShowAsync(dialog, CancellationToken.None);

            dialog.Closing += (s, e) =>
            {
                if (e.Result == ContentDialogResult.Primary)
                {
                    // LOGIC MỚI:
                    // 1. Gọi Service chuyển từ Pending -> Saved JSON
                    _dataService.MoveToSaved(SelectedProfile);

                    // 2. Xóa khỏi giao diện trang Home
                    Profiles.Remove(SelectedProfile);
                    Count--;
                }
                else
                {
                    SelectedProfile = null;
                }
            };
        }

        // XỬ LÝ NÚT GHI (RECORD)
        [RelayCommand]
        private void RecordAsync()
        {
            if (IsPlaying)
            {
                MessageBox.Show("Đang chạy auto, vui lòng dừng chạy trước khi ghi âm.", "Thông báo");
                return;
            }

            // Đảo trạng thái: Nếu đang tắt thì bật, đang bật thì tắt
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

        // XỬ LÝ NÚT CHẠY (PLAY)
        [RelayCommand]
        private async Task PlayAsync()
        {
            if (IsRecording)
            {
                MessageBox.Show("Đang trong chế độ Ghi âm. Vui lòng dừng ghi trước.", "Cảnh báo");
                return;
            }

            // Nếu chưa chạy -> Bắt đầu chạy
            if (!IsPlaying)
            {
                IsPlaying = true; // Cập nhật giao diện (Icon đổi thành Stop)
                _cts = new CancellationTokenSource();

                try
                {
                    // Gọi Service để chạy lại thao tác
                    // Truyền Token vào để khi bấm Stop thì hàm này sẽ dừng lại ngay
                    await _recordService.PlayRecordingAsync(_cts.Token);
                }
                catch (TaskCanceledException)
                {
                    // Bắt lỗi khi người dùng bấm Dừng (không làm gì cả)
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi xảy ra: {ex.Message}", "Lỗi");
                }
                finally
                {
                    // Dù chạy xong hay bị hủy, luôn reset trạng thái về ban đầu
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
                // Gửi yêu cầu hủy tác vụ
                _cts?.Cancel();
            }
        }
    }
}