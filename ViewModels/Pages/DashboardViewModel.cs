using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using ToolVip.Models;
using ToolVip.Services;
using ToolVip.Views.UseControls;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;

namespace ToolVip.ViewModels.Pages
{
    public partial class DashboardViewModel : ObservableObject, INavigationAware
    {
        private readonly IContentDialogService _contentDialogService;
        private readonly IDataService _dataService;

        [ObservableProperty]
        private ObservableCollection<DriverProfile> _profiles = new();

        [ObservableProperty]
        private DriverProfile? _selectedProfile;

        [ObservableProperty]
        private bool _isRecording = false;

        [ObservableProperty]
        private bool _isPlaying = false;

        [ObservableProperty]
        private int? _count = 0;

        public DashboardViewModel(IContentDialogService contentDialogService, IDataService dataService)
        {
            _contentDialogService = contentDialogService;
            _dataService = dataService;

            // XÓA HẾT DỮ LIỆU GIẢ LẬP CŨ Ở ĐÂY
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

        [RelayCommand]
        private async void RecordAsync()
        {
            IsRecording = !IsRecording;
            
        }

        [RelayCommand]
        private async void PlayAsync()
        {
            IsPlaying = !IsPlaying;
        }
    }
}