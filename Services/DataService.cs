using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ToolVip.Models;

namespace ToolVip.Services
{
    public interface IDataService
    {
        // Folder UserData
        void EnsureUserDataFolder();

        // Xử lý danh sách Chờ (Pending) - Hiển thị ở Home
        List<DriverProfile> LoadPendingData();
        void AddToPending(List<DriverProfile> profiles);
        void RemoveFromPending(DriverProfile profile);

        // Xử lý danh sách Đã lưu (Saved) - Hiển thị ở trang Đã lưu
        List<DriverProfile> LoadSavedData();
        void MoveToSaved(DriverProfile profile); // Chuyển từ Chờ sang Đã lưu

        // [MỚI] Xử lý File Tạm (Temp/Backup) - Tránh mất dữ liệu
        void SaveToTemp(List<DriverProfile> profiles); // Lưu danh sách Import tạm thời
        List<DriverProfile> LoadTempData();
        void BackupSingleProfile(DriverProfile profile); // Backup 1 hồ sơ đang xem
    }

    public class DataService : IDataService
    {
        private readonly string _userDataFolder;
        private readonly string _pendingPath;
        private readonly string _savedPath;
        private readonly string _tempImportPath;  // File tạm khi mới lấy về
        private readonly string _backupSinglePath; // File backup hồ sơ đang mở
        private readonly JsonSerializerOptions _options;

        public DataService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // [CẬP NHẬT] Gom tất cả vào folder UserData
            _userDataFolder = Path.Combine(baseDir, "UserData");

            _pendingPath = Path.Combine(_userDataFolder, "pending_profiles.json");
            _savedPath = Path.Combine(_userDataFolder, "saved_profiles.json");
            _tempImportPath = Path.Combine(_userDataFolder, "temp_import.json");
            _backupSinglePath = Path.Combine(_userDataFolder, "current_viewing_backup.json");

            // Cấu hình để không lỗi font tiếng Việt
            _options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };

            EnsureUserDataFolder();
        }

        public void EnsureUserDataFolder()
        {
            if (!Directory.Exists(_userDataFolder))
            {
                Directory.CreateDirectory(_userDataFolder);
            }
        }

        // --- KHU VỰC XỬ LÝ FILE PENDING (CHO DASHBOARD) ---

        public List<DriverProfile> LoadPendingData()
        {
            return LoadFile(_pendingPath);
        }

        public void AddToPending(List<DriverProfile> profiles)
        {
            var current = LoadPendingData();
            // Có thể thêm logic check trùng ở đây nếu cần
            current.AddRange(profiles);
            SaveFile(_pendingPath, current);

            // Sau khi đã lưu vào DB chính (Pending), có thể xóa file temp import đi cho sạch
            if (File.Exists(_tempImportPath)) File.Delete(_tempImportPath);
        }

        public void RemoveFromPending(DriverProfile profile)
        {
            var current = LoadPendingData();
            // So sánh theo LicensePlate (_id) hoặc CCCD
            var itemToRemove = current.FirstOrDefault(x => x.LicensePlate == profile.LicensePlate || x.Cccd == profile.Cccd);
            if (itemToRemove != null)
            {
                current.Remove(itemToRemove);
                SaveFile(_pendingPath, current);
            }
        }

        // --- KHU VỰC XỬ LÝ FILE SAVED (CHO TRANG ĐÃ LƯU) ---

        public List<DriverProfile> LoadSavedData()
        {
            return LoadFile(_savedPath);
        }

        public void MoveToSaved(DriverProfile profile)
        {
            // 1. Thêm vào file Saved
            var savedList = LoadSavedData();

            //savedList.Add(profile);
            savedList.Insert(0, profile);
            SaveFile(_savedPath, savedList);

            // 2. Xóa khỏi file Pending
            RemoveFromPending(profile);

            // 3. Xóa backup single vì đã xử lý xong
            if (File.Exists(_backupSinglePath)) File.Delete(_backupSinglePath);
        }

        // --- [MỚI] KHU VỰC XỬ LÝ FILE TEMP / BACKUP ---

        public void SaveToTemp(List<DriverProfile> profiles)
        {
            // Lưu ngay lập tức khi vừa lấy từ API hoặc Excel về
            SaveFile(_tempImportPath, profiles);
        }

        public List<DriverProfile> LoadTempData()
        {
            return LoadFile(_tempImportPath);
        }

        public void BackupSingleProfile(DriverProfile profile)
        {
            // Lưu 1 hồ sơ đang mở ra file riêng, lỡ app crash thì còn file này
            var list = new List<DriverProfile> { profile };
            SaveFile(_backupSinglePath, list);
        }

        // --- HÀM HỖ TRỢ CHUNG ---

        private List<DriverProfile> LoadFile(string path)
        {
            if (!File.Exists(path)) return new List<DriverProfile>();
            try
            {
                var json = File.ReadAllText(path);
                return JsonSerializer.Deserialize<List<DriverProfile>>(json) ?? new List<DriverProfile>();
            }
            catch { return new List<DriverProfile>(); }
        }

        private void SaveFile(string path, List<DriverProfile> data)
        {
            try
            {
                EnsureUserDataFolder();
                var json = JsonSerializer.Serialize(data, _options);
                File.WriteAllText(path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Lỗi lưu file {path}: {ex.Message}");
            }
        }
    }
}