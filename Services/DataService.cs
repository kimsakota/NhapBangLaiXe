using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ToolVip.Models;

namespace ToolVip.Services
{
    public interface IDataService
    {
        // Xử lý danh sách Chờ (Pending) - Hiển thị ở Home
        List<DriverProfile> LoadPendingData();
        void AddToPending(List<DriverProfile> profiles);
        void RemoveFromPending(DriverProfile profile);

        // Xử lý danh sách Đã lưu (Saved) - Hiển thị ở trang Đã lưu
        List<DriverProfile> LoadSavedData();
        void MoveToSaved(DriverProfile profile); // Chuyển từ Chờ sang Đã lưu
    }

    public class DataService : IDataService
    {
        private readonly string _pendingPath;
        private readonly string _savedPath;
        private readonly JsonSerializerOptions _options;

        public DataService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _pendingPath = Path.Combine(baseDir, "pending_profiles.json"); // File cho trang Home
            _savedPath = Path.Combine(baseDir, "saved_profiles.json");     // File cho trang Đã lưu

            // Cấu hình để không lỗi font tiếng Việt
            _options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
            };
        }

        // --- KHU VỰC XỬ LÝ FILE PENDING (CHO DASHBOARD) ---

        public List<DriverProfile> LoadPendingData()
        {
            return LoadFile(_pendingPath);
        }

        public void AddToPending(List<DriverProfile> profiles)
        {
            var current = LoadPendingData();
            current.AddRange(profiles);
            SaveFile(_pendingPath, current);
        }

        public void RemoveFromPending(DriverProfile profile)
        {
            var current = LoadPendingData();
            // Tìm và xóa item tương ứng (so sánh đơn giản, hoặc cần ID nếu có trùng lặp)
            var itemToRemove = current.FirstOrDefault(x => x.Cccd == profile.Cccd && x.LicensePlate == profile.LicensePlate);
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
            savedList.Add(profile);
            SaveFile(_savedPath, savedList);

            // 2. Xóa khỏi file Pending
            RemoveFromPending(profile);
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
            var json = JsonSerializer.Serialize(data, _options);
            File.WriteAllText(path, json);
        }
    }
}