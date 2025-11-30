using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ToolVip.Models;

namespace ToolVip.Services
{
    public interface IApiService
    {
        bool IsLoggedIn { get; }
        Task<bool> LoginAsync(string username, string password);
        Task<List<DriverProfile>> GetProfilesAsync();
        Task<bool> ImportProfileAsync(DriverProfile profile);
    }

    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private string _authToken = "";

        // [LƯU Ý] Kiểm tra lại URL chính xác (http hay https)
        private const string BASE_URL = "https://service.vst.edu.vn";

        public bool IsLoggedIn => !string.IsNullOrEmpty(_authToken);

        public ApiService()
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(BASE_URL),
                Timeout = TimeSpan.FromSeconds(30)
            };
        }

        public async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                // Tạo cấu trúc: { "value": { "username": "...", "password": "..." } }
                var credentials = new LoginCredentials { Username = username, Password = password };
                var payload = new ApiWrapper<LoginCredentials>(credentials);

                var response = await _httpClient.PostAsJsonAsync("/api/guest/login", payload);

                if (response.IsSuccessStatusCode)
                {
                    // Đọc phản hồi: { "value": { "token": "...", ... } }
                    var resultWrapper = await response.Content.ReadFromJsonAsync<ApiWrapper<LoginResult>>();

                    if (resultWrapper?.Value != null && !string.IsNullOrEmpty(resultWrapper.Value.Token))
                    {
                        _authToken = resultWrapper.Value.Token;
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Login Error: {ex.Message}");
                return false;
            }
        }

        public async Task<List<DriverProfile>> GetProfilesAsync()
        {
            if (!IsLoggedIn) return new List<DriverProfile>();

            try
            {
                // API: /api/xin/5 (Theo ví dụ của bạn) hoặc /api/staff/xin
                // Tôi dùng /api/staff/xin theo yêu cầu ban đầu, nếu lỗi bạn đổi thành /api/xin/5
                var response = await _httpClient.GetAsync("/api/xin/5");

                if (response.IsSuccessStatusCode)
                {
                    // JSON: { "value": [ { ... }, { ... } ] }
                    var wrapper = await response.Content.ReadFromJsonAsync<ApiWrapper<List<DriverProfileDto>>>();

                    if (wrapper?.Value != null)
                    {
                        // Map từ DTO (API) sang Model (App)
                        return wrapper.Value.Select(dto => new DriverProfile
                        {
                            FullName = dto.Name,
                            Cccd = dto.Cccd,
                            IssueDate = dto.NgayCap,
                            PhoneNumber = dto.SoDT,
                            Address = dto.Dctt,          // ĐCTT -> Address
                            WardCommune = dto.SoNha,     // Số nhà -> WardCommune (hoặc ngược lại tùy bạn quy định)
                            EngineNumber = dto.SoMay,
                            ChassisNumber = dto.SoKhung,
                            LicensePlate = "",           // API chưa thấy trả về Biển số?
                        }).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get Data Error: {ex.Message}");
            }
            return new List<DriverProfile>();
        }

        public async Task<bool> ImportProfileAsync(DriverProfile profile)
        {
            if (!IsLoggedIn) return false;

            try
            {
                // Map từ Model (App) sang DTO (API) để gửi đi
                var dto = new DriverProfileDto
                {
                    Name = profile.FullName ?? "",
                    Cccd = profile.Cccd ?? "",
                    NgayCap = profile.IssueDate ?? "",
                    SoDT = profile.PhoneNumber ?? "",
                    Dctt = profile.Address ?? "",
                    SoNha = profile.WardCommune ?? "",
                    SoMay = profile.EngineNumber ?? "",
                    SoKhung = profile.ChassisNumber ?? ""
                    // Các trường khác như _id, actor... server có thể tự sinh hoặc lấy từ token
                };

                // Bọc trong { "value": ... }
                var payload = new ApiWrapper<DriverProfileDto>(dto);

                var response = await _httpClient.PostAsJsonAsync("/api/staff/nhap", payload);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
}