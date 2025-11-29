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

                // Tách dòng
                var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

                // Nếu chỉ có 1 dòng (tiêu đề) thì báo lỗi
                if (lines.Length <= 1)
                {
                    MessageBox.Show("Dữ liệu quá ít (chỉ có tiêu đề hoặc trống).", "Lỗi");
                    return;
                }

                ImportedProfiles.Clear();

                // Vòng lặp bắt đầu từ i = 1 (BỎ QUA DÒNG TIÊU ĐỀ)
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var parts = line.Split('\t');

                    try
                    {
                        // --- MAPPING DỮ LIỆU TỪ EXCEL (CỘT 2 -> CỘT 11) ---
                        // Giả sử copy cả cột STT (Cột 1 - Index 0) thì ta bắt đầu lấy từ Index 1.

                        var profile = new DriverProfile
                        {
                            // Cột 2: Họ tên
                            FullName = GetPart(parts, 2),

                            // Cột 3: Số CCCD
                            Cccd = GetPart(parts, 3),

                            // Số điện thoại (PhoneNumber) - (Giả sử cột này là SĐT)
                            PhoneNumber = GetPart(parts, 4, "0342036732"),

                            // Cột 4: Ngày cấp (Issue Date)
                            IssueDate = GetPart(parts, 5),

                            // Cột 5: Địa chỉ thường trú (Address)
                            Address = GetPart(parts, 6),

                            // Cột 6: Số nhà / Xã phường (WardCommune)
                            WardCommune = GetPart(parts, 7),

                            // Cột 7: Biển số (LicensePlate)
                            LicensePlate = GetPart(parts, 8),

                            // Cột 8: Số máy (EngineNumber)
                            EngineNumber = GetPart(parts, 9),

                            // Cột 9: Số khung (ChassisNumber)
                            ChassisNumber = GetPart(parts, 10),

                            

                            // Cột 11: (Dư thừa hoặc Ghi chú) - Nếu cần lấy thêm thì gán vào đâu đó
                            // Ví dụ: Nếu Excel có cột ghi chú, có thể nối vào địa chỉ
                            // Note = GetPart(parts, 10) 
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

            // Hàm này sẽ tự động lưu vào JSON thông qua DataService
            _dataService.AddToPending(ImportedProfiles.ToList());

            MessageBox.Show($"Đã lưu {ImportedProfiles.Count} hồ sơ vào hệ thống!", "Thành công");
            ImportedProfiles.Clear();
        }

        private string GetPart(string[] parts, int index, string defaultValue = "")
        {
            if (index >= 0 && index < parts.Length)
            {
                var val = parts[index].Trim();
                // Nếu cắt khoảng trắng xong mà rỗng thì trả về mặc định
                return string.IsNullOrEmpty(val) ? defaultValue : val;
            }
            return defaultValue; // Trả về mặc định nếu index không tồn tại
        }
    }
}