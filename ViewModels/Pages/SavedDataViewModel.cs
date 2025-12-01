using System.Collections.ObjectModel;
using System.Windows;
using ToolVip.Models;
using ToolVip.Services;
using ToolVip.Views.UseControls;
using Wpf.Ui;
using Wpf.Ui.Abstractions.Controls;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace ToolVip.ViewModels.Pages
{
    public partial class SavedDataViewModel : ObservableObject, INavigationAware
    {
        private readonly IContentDialogService _contentDialogService;
        private readonly IDataService _dataService;
        private readonly IApiService _apiService;

        // Danh sách gốc (không đổi)
        private List<DriverProfile> _allProfiles = new();

        [ObservableProperty]
        private ObservableCollection<DriverProfile> _savedProfiles = new();

        [ObservableProperty]
        private DriverProfile? _selectedProfile;

        [ObservableProperty]
        private int? _count = 0;

        // [MỚI] Từ khóa tìm kiếm
        [ObservableProperty]
        private string _searchKeyword = "";

        public SavedDataViewModel(IDataService dataService,
            IContentDialogService contentDialogService,
            IApiService apiService)
        {
            _dataService = dataService;
            _contentDialogService = contentDialogService;
            _apiService = apiService;

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

            var dialogControl = new SavedDetailDialog { DataContext = SelectedProfile };
            var dialog = new ContentDialog
            {
                Title = $"Đã lưu: {SelectedProfile.LicensePlate}",
                Content = dialogControl,
                PrimaryButtonText = "Đồng bộ Server",
                CloseButtonText = "Đóng",
                DefaultButton = ContentDialogButton.Close,
            };

            _contentDialogService.ShowAsync(dialog, CancellationToken.None);

            dialog.Closing += async (s, e) =>
            {
                if (e.Result == ContentDialogResult.Primary)
                    await SyncOneProfileAsync();
                else
                    SelectedProfile = null;
            };
        }

        // [MỚI] Tự động filter khi SearchKeyword thay đổi
        partial void OnSearchKeywordChanged(string value)
        {
            FilterProfiles();
        }

        private void LoadData()
        {
            var data = _dataService.LoadSavedData();
            _allProfiles = data;
            FilterProfiles();
        }

        // [MỚI] Hàm filter danh sách
        private void FilterProfiles()
        {
            if (string.IsNullOrWhiteSpace(SearchKeyword))
            {
                // Không có từ khóa -> Hiển thị tất cả
                SavedProfiles = new ObservableCollection<DriverProfile>(_allProfiles);
            }
            else
            {
                // Có từ khóa -> Lọc theo Biển số, Họ tên, CCCD, SĐT
                var keyword = SearchKeyword.Trim().ToLower();
                var filtered = _allProfiles.Where(p =>
                    (!string.IsNullOrEmpty(p.LicensePlate) && p.LicensePlate.ToLower().Contains(keyword)) ||
                    (!string.IsNullOrEmpty(p.FullName) && p.FullName.ToLower().Contains(keyword)) ||
                    (!string.IsNullOrEmpty(p.Cccd) && p.Cccd.ToLower().Contains(keyword)) ||
                    (!string.IsNullOrEmpty(p.PhoneNumber) && p.PhoneNumber.ToLower().Contains(keyword)) || 
                    (!string.IsNullOrEmpty(p.WardCommune) && p.WardCommune.ToLower().Contains(keyword))
                ).ToList();

                SavedProfiles = new ObservableCollection<DriverProfile>(filtered);
            }

            Count = SavedProfiles.Count;
        }

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

        [RelayCommand]
        private async Task SyncOneProfileAsync()
        {
            if (!_apiService.IsLoggedIn)
            {
                MessageBox.Show("Bạn chưa đăng nhập API.", "Cảnh báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (SelectedProfile == null)
            {
                MessageBox.Show("Vui lòng chọn (click vào) 1 hồ sơ trong danh sách để đồng bộ.", "Chưa chọn hồ sơ", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (string.IsNullOrEmpty(SelectedProfile.LicensePlate))
            {
                MessageBox.Show("Hồ sơ này bị lỗi: Không có biển số.", "Lỗi");
                return;
            }

            bool result = await _apiService.ConfirmImportedAsync(SelectedProfile.LicensePlate);

            if (result)
            {
                MessageBox.Show($"Đồng bộ thành công biển số: {SelectedProfile.LicensePlate}", "Thành công");
            }
            else
            {
                MessageBox.Show($"Đồng bộ THẤT BẠI biển số: {SelectedProfile.LicensePlate}\n(Có thể do lỗi mạng hoặc Token hết hạn)", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}