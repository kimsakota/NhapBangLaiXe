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

        [ObservableProperty]
        private ObservableCollection<DriverProfile> _importedProfiles = new();

        public ImportViewModel(IDataService dataService)
        {
            _dataService = dataService;
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

                // Nếu chỉ có 1 dòng (tiêu đề) thì không làm gì cả
                if (lines.Length <= 1)
                {
                    MessageBox.Show("Dữ liệu quá ít (chỉ có tiêu đề hoặc trống).", "Lỗi");
                    return;
                }

                ImportedProfiles.Clear();

                // Vòng lặp bắt đầu từ i = 1 (BỎ QUA DÒNG ĐẦU TIÊN - TIÊU ĐỀ)
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('\t');

                    try
                    {
                        // LẤY DỮ LIỆU THEO CỘT CỐ ĐỊNH BẠN YÊU CẦU:
                        // Cột 2 (Index 1) -> Họ tên
                        // Cột 3 (Index 2) -> CCCD
                        // Cột 5 (Index 4) -> Điện thoại
                        // Cột 7 (Index 6) -> Địa chỉ
                        // Cột 8 (Index 7) -> Xã/Phường (hoặc phần mở rộng địa chỉ)
                        // Cột 9 (Index 8) -> Biển số
                        // Cột 10 (Index 9) -> Số máy
                        // Cột 11 (Index 10) -> Số khung

                        var profile = new DriverProfile
                        {
                            FullName = GetPart(parts, 1),
                            Cccd = GetPart(parts, 2),
                            PhoneNumber = GetPart(parts, 4),
                            Address = GetPart(parts, 6),
                            WardCommune = GetPart(parts, 7),
                            LicensePlate = GetPart(parts, 8),
                            EngineNumber = GetPart(parts, 9),
                            ChassisNumber = GetPart(parts, 10),

                            IssueDate = DateTime.Now.ToString("dd/MM/yyyy")
                        };

                        // Chỉ thêm nếu có ít nhất Tên hoặc Biển số (tránh dòng trống)
                        if (!string.IsNullOrWhiteSpace(profile.FullName) || !string.IsNullOrWhiteSpace(profile.LicensePlate))
                        {
                            ImportedProfiles.Add(profile);
                        }
                    }
                    catch { }
                }

                if (ImportedProfiles.Count == 0)
                {
                    MessageBox.Show("Không lấy được dữ liệu nào. Vui lòng kiểm tra vùng copy có đủ cột không.", "Thông báo");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

        // Lệnh mới: Xóa dữ liệu trên lưới
        [RelayCommand]
        private void OnClearData()
        {
            if (ImportedProfiles.Count > 0)
            {
                var result = MessageBox.Show("Bạn có chắc muốn xóa toàn bộ danh sách đang hiển thị?", "Xác nhận", System.Windows.MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == System.Windows.MessageBoxResult.Yes)
                {
                    ImportedProfiles.Clear();
                }
            }
        }

        [RelayCommand]
        private void OnSaveToDatabase()
        {
            if (ImportedProfiles.Count == 0) return;

            // Lưu vào danh sách chờ (Pending)
            _dataService.AddToPending(ImportedProfiles.ToList());

            MessageBox.Show($"Đã thêm {ImportedProfiles.Count} hồ sơ vào Trang chủ!", "Thành công");
            ImportedProfiles.Clear();
        }

        private string GetPart(string[] parts, int index)
        {
            if (index >= 0 && index < parts.Length)
            {
                return parts[index].Trim();
            }
            return "";
        }
    }
}