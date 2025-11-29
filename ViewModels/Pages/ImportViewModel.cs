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

                // Vòng lặp bắt đầu từ i = 1 (TRỪ DÒNG TIÊU ĐỀ)
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('\t');

                    try
                    {
                        // MAPPING DỮ LIỆU TỪ EXCEL (BỎ CỘT ĐẦU TIÊN - STT)
                        // parts[0]: STT (Bỏ qua)
                        // parts[1]: Họ tên
                        // parts[2]: CCCD
                        // parts[3]: Ngày sinh (Bỏ qua hoặc map nếu muốn)
                        // parts[4]: SĐT
                        // parts[5]: Địa chỉ
                        // parts[6]: Xã/Phường
                        // parts[7]: Biển số
                        // parts[8]: Số máy
                        // parts[9]: Số khung

                        var profile = new DriverProfile
                        {
                            FullName = GetPart(parts, 1),      // Cột 2 Excel
                            Cccd = GetPart(parts, 2),          // Cột 3 Excel
                            PhoneNumber = GetPart(parts, 4),   // Cột 5 Excel
                            Address = GetPart(parts, 5),       // Cột 6 Excel
                            WardCommune = GetPart(parts, 6),   // Cột 7 Excel
                            LicensePlate = GetPart(parts, 7),  // Cột 8 Excel
                            EngineNumber = GetPart(parts, 8),  // Cột 9 Excel
                            ChassisNumber = GetPart(parts, 9), // Cột 10 Excel

                            // Ngày cấp hiện tại để mặc định là ngày nhập, 
                            // nếu muốn lấy ngày sinh từ Excel thì dùng: GetPart(parts, 3)
                            IssueDate = DateTime.Now.ToString("dd/MM/yyyy")
                        };

                        // Kiểm tra dữ liệu rác: Phải có Tên hoặc Biển số mới thêm
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