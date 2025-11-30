using System.Collections.ObjectModel;
using System.Windows; // Thêm để dùng MessageBox
using ToolVip.Models;
using ToolVip.Services;
using ToolVip.Views.UseControls;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult; // Định nghĩa rõ MessageBox

namespace ToolVip.ViewModels.Pages
{
    public partial class SavedDataViewModel : ObservableObject, INavigationAware
    {
        private readonly IContentDialogService _contentDialogService;
        private readonly IDataService _dataService;
        private readonly IApiService _apiService; // [MỚI] Thêm ApiService

        [ObservableProperty]
        private ObservableCollection<DriverProfile> _savedProfiles = new();

        [ObservableProperty]
        private DriverProfile? _selectedProfile;

        [ObservableProperty]
        private int? _count = 0;

        // [CẬP NHẬT] Thêm tham số apiService vào Constructor
        public SavedDataViewModel(IDataService dataService,
            IContentDialogService contentDialogService,
            IApiService apiService)
        {
            _dataService = dataService;
            _contentDialogService = contentDialogService;
            _apiService = apiService; // [MỚI]

            LoadData();
        }

        public Task OnNavigatedToAsync()
        {
            LoadData();
            return Task.CompletedTask;
        }

        public Task OnNavigatedFromAsync() => Task.CompletedTask;

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
                    SavedProfiles.Remove(SelectedProfile);
                    Count--;
                }
                else
                {
                    SelectedProfile = null;
                }
            };
        }

        private void LoadData()
        {
            var data = _dataService.LoadSavedData();
            SavedProfiles = new ObservableCollection<DriverProfile>(data);
            Count = SavedProfiles.Count;
        }

        // [MỚI] Hàm xử lý gửi lại danh sách lên Server
        [RelayCommand]
        private async Task SyncToServerAsync()
        {
            if (!_apiService.IsLoggedIn)
            {
                MessageBox.Show("Bạn chưa đăng nhập API (Tab Nhập).\nVui lòng đăng nhập trước khi đồng bộ.", "Chưa đăng nhập", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SavedProfiles.Count == 0)
            {
                MessageBox.Show("Danh sách trống.", "Thông báo");
                return;
            }

            var confirm = MessageBox.Show($"Bạn có muốn gửi lại toàn bộ {SavedProfiles.Count} hồ sơ này lên Server không?", "Xác nhận đồng bộ", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            int success = 0;
            int fail = 0;

            // Duyệt qua từng hồ sơ và gửi
            foreach (var profile in SavedProfiles)
            {
                if (!string.IsNullOrEmpty(profile.LicensePlate))
                {
                    bool result = await _apiService.ConfirmImportedAsync(profile.LicensePlate);
                    if (result) success++;
                    else fail++;
                }
            }

            MessageBox.Show($"Đồng bộ hoàn tất:\n- Thành công: {success}\n- Thất bại: {fail}", "Kết quả", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}