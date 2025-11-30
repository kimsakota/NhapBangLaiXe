using System.Collections.ObjectModel;
using System.Windows;
using ToolVip.Models;
using ToolVip.Services;
using Wpf.Ui.Controls;
using MessageBox = System.Windows.MessageBox;

namespace ToolVip.ViewModels.Pages
{
    public partial class ImportViewModel : ObservableObject
    {
        private readonly IDataService _dataService;
        private readonly IApiService _apiService;

        [ObservableProperty]
        private ObservableCollection<DriverProfile> _importedProfiles = new();

        [ObservableProperty] private string _username = "0384022083";
        [ObservableProperty] private string _password = "2083";
        [ObservableProperty] private bool _isLoggedIn = false;
        [ObservableProperty] private bool _isBusy = false;

        public ImportViewModel(IDataService dataService, IApiService apiService)
        {
            _dataService = dataService;
            _apiService = apiService;

            // Tự động load lại dữ liệu temp nếu có (phòng trường hợp crash)
            LoadTempData();
        }

        private void LoadTempData()
        {
            var tempData = _dataService.LoadTempData();
            if (tempData != null && tempData.Count > 0)
            {
                ImportedProfiles = new ObservableCollection<DriverProfile>(tempData);
            }
        }

        [RelayCommand]
        private async Task LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
            {
                MessageBox.Show("Vui lòng nhập tài khoản và mật khẩu!", "Thông báo");
                return;
            }

            IsBusy = true;
            bool success = await _apiService.LoginAsync(Username, Password);
            IsBusy = false;

            if (success)
            {
                IsLoggedIn = true;
                MessageBox.Show("Đăng nhập thành công!", "Thông báo");
            }
            else
            {
                MessageBox.Show("Đăng nhập thất bại. Vui lòng kiểm tra lại!", "Lỗi");
            }
        }

        [RelayCommand]
        private async Task GetDataFromApiAsync()
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show("Bạn chưa đăng nhập API.", "Cảnh báo");
                return;
            }

            IsBusy = true;
            var data = await _apiService.GetProfilesAsync();
            IsBusy = false;

            if (data != null && data.Count > 0)
            {
                ImportedProfiles.Clear();
                foreach (var item in data)
                {
                    ImportedProfiles.Add(item);
                }

                // [QUAN TRỌNG] Lưu ngay vào Temp sau khi lấy về để tránh mất
                _dataService.SaveToTemp(ImportedProfiles.ToList());

                MessageBox.Show($"Đã lấy được {data.Count} hồ sơ từ Server (Đã lưu Temp).", "Thành công");
            }
            else
            {
                MessageBox.Show("Không có dữ liệu mới từ Server.", "Thông báo");
            }
        }

        [RelayCommand]
        private async Task UploadToApiAsync()
        {
            if (!IsLoggedIn)
            {
                MessageBox.Show("Bạn chưa đăng nhập API.", "Cảnh báo");
                return;
            }

            if (ImportedProfiles.Count == 0)
            {
                MessageBox.Show("Danh sách trống.", "Cảnh báo");
                return;
            }

            IsBusy = true;
            int successCount = 0;
            int failCount = 0;

            foreach (var profile in ImportedProfiles.ToList())
            {
                bool result = await _apiService.ImportProfileAsync(profile);
                if (result) successCount++;
                else failCount++;
            }
            IsBusy = false;

            MessageBox.Show($"Đã gửi xong.\n- Thành công: {successCount}\n- Thất bại: {failCount}", "Kết quả");
        }

        [RelayCommand]
        private void OnPasteFromClipboard()
        {
            try
            {
                var text = System.Windows.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                {
                    MessageBox.Show("Clipboard trống! Hãy copy lại dữ liệu từ Excel.", "Thông báo");
                    return;
                }

                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                if (lines.Length <= 1)
                {
                    MessageBox.Show("Dữ liệu quá ít (chỉ có tiêu đề hoặc trống).", "Lỗi");
                    return;
                }

                ImportedProfiles.Clear();

                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('\t');

                    try
                    {
                        var profile = new DriverProfile
                        {
                            FullName = GetPart(parts, 2),
                            Cccd = GetPart(parts, 3),
                            PhoneNumber = GetPart(parts, 4, "0342036732"),
                            IssueDate = GetPart(parts, 5),
                            Address = GetPart(parts, 6),
                            WardCommune = GetPart(parts, 7),
                            LicensePlate = GetPart(parts, 8),
                            EngineNumber = GetPart(parts, 9),
                            ChassisNumber = GetPart(parts, 10),
                        };

                        if (!string.IsNullOrWhiteSpace(profile.FullName) || !string.IsNullOrWhiteSpace(profile.LicensePlate))
                        {
                            ImportedProfiles.Add(profile);
                        }
                    }
                    catch { }
                }

                // [QUAN TRỌNG] Lưu ngay vào Temp sau khi Paste
                if (ImportedProfiles.Count > 0)
                {
                    _dataService.SaveToTemp(ImportedProfiles.ToList());
                }
                else
                {
                    MessageBox.Show("Không lấy được dữ liệu nào. Vui lòng kiểm tra vùng copy có đủ cột không.", "Thông báo");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

        [RelayCommand]
        private void OnClearData()
        {
            if (ImportedProfiles.Count > 0)
            {
                var result = MessageBox.Show("Bạn có chắc muốn xóa toàn bộ danh sách đang hiển thị?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    ImportedProfiles.Clear();
                    // Lưu danh sách rỗng vào Temp (coi như xóa)
                    _dataService.SaveToTemp(new List<DriverProfile>());
                }
            }
        }

        [RelayCommand]
        private void OnSaveToDatabase()
        {
            if (ImportedProfiles.Count == 0) return;

            // Lưu vào Pending chính thức
            _dataService.AddToPending(ImportedProfiles.ToList());

            MessageBox.Show($"Đã lưu {ImportedProfiles.Count} hồ sơ vào hệ thống (Local)!", "Thành công");
            ImportedProfiles.Clear();

            // Temp import đã được xử lý trong AddToPending (xóa file temp)
        }

        private string GetPart(string[] parts, int index, string defaultValue = "")
        {
            if (index >= 0 && index < parts.Length)
            {
                var val = parts[index].Trim();
                return string.IsNullOrEmpty(val) ? defaultValue : val;
            }
            return defaultValue;
        }
    }
}