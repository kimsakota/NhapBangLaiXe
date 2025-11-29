using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ToolVip.Models
{
    public class DriverProfile
    {
        public string? FullName { get; set; }        // Họ tên
        public string? Cccd { get; set; }            // Số CCCD
        public string? IssueDate { get; set; }       // Ngày cấp
        public string? PhoneNumber { get; set; }     // Điện thoại
        public string? WardCommune { get; set; }     // Xã/Phường
        public string? Address { get; set; }         // Địa chỉ chi tiết
        public string? LicensePlate { get; set; }    // Biển số
        public string? EngineNumber { get; set; }    // Số máy
        public string? ChassisNumber { get; set; }   // Số khung
    }
}
