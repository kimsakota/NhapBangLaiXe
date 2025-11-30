using System.Text.Json.Serialization;

namespace ToolVip.Models
{
    // --- WRAPPER CHUNG ---
    // Vì mọi request/response đều bọc trong object "value"
    public class ApiWrapper<T>
    {
        [JsonPropertyName("value")]
        public T Value { get; set; }

        public ApiWrapper() { }
        public ApiWrapper(T value) { Value = value; }
    }

    // --- LOGIN ---

    // Dữ liệu đăng nhập bên trong (User/Pass)
    public class LoginCredentials
    {
        [JsonPropertyName("username")]
        public string Username { get; set; } = "";

        [JsonPropertyName("password")]
        public string Password { get; set; } = "";
    }

    // Kết quả đăng nhập trả về bên trong
    public class LoginResult
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("role")]
        public string Role { get; set; } = "";

        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("token")]
        public string Token { get; set; } = "";
    }

    // --- DRIVER DATA ---

    // DTO hứng dữ liệu hồ sơ từ API (Khớp với JSON trả về)
    public class DriverProfileDto
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }        // Map sang FullName

        [JsonPropertyName("cccd")]
        public string Cccd { get; set; }

        [JsonPropertyName("ngayCap")]
        public string NgayCap { get; set; }     // Map sang IssueDate

        [JsonPropertyName("soDT")]
        public string SoDT { get; set; }        // Map sang PhoneNumber

        [JsonPropertyName("dctt")]
        public string Dctt { get; set; }        // Map sang Address

        [JsonPropertyName("soNha")]
        public string SoNha { get; set; }       // Map sang WardCommune

        [JsonPropertyName("soMay")]
        public string SoMay { get; set; }       // Map sang EngineNumber

        [JsonPropertyName("soKhung")]
        public string SoKhung { get; set; }     // Map sang ChassisNumber

        [JsonPropertyName("actor")]
        public string Actor { get; set; }
    }
}