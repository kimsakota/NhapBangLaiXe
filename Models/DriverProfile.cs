namespace ToolVip.Models
{
    public class DriverProfile
    {
        public string? FullName { get; set; }        // Họ tên (Excel Cột 2)
        public string? Cccd { get; set; }            // Số CCCD (Excel Cột 3)
        public string? IssueDate { get; set; }       // Ngày cấp (Tự sinh hoặc Excel Cột 4 nếu cần)
        public string? PhoneNumber { get; set; }     // Điện thoại (Excel Cột 5)
        public string? Address { get; set; }         // Địa chỉ chi tiết (Excel Cột 6)
        public string? WardCommune { get; set; }     // Xã/Phường (Excel Cột 7)
        public string? LicensePlate { get; set; }    // Biển số (Excel Cột 8)
        public string? EngineNumber { get; set; }    // Số máy (Excel Cột 9)
        public string? ChassisNumber { get; set; }   // Số khung (Excel Cột 10)
    }
}