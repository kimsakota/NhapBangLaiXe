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
        string CurrentRole { get; }
        Task<bool> LoginAsync(string username, string password);
        Task<List<DriverProfile>> GetProfilesAsync(int limit = 1);
        Task<bool> ImportProfileAsync(DriverProfile profile);
        Task<bool> ConfirmImportedAsync(string licensePlate);

        Task<bool> CheckConnectionAsync();
    }

    public class ApiService : IApiService
    {
        private readonly HttpClient _httpClient;
        private string _authToken = "";
        private string _currentRole = "";

        private const string BASE_URL = "https://service.vst.edu.vn";

        public bool IsLoggedIn => !string.IsNullOrEmpty(_authToken);
        public string CurrentRole => _currentRole;

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
                var credentials = new LoginCredentials { Username = username, Password = password };
                // Login vẫn dùng cấu trúc cũ hoặc wrapper tùy server, 
                // nhưng thường login trả về token nên ta giữ nguyên logic này 
                // hoặc sửa nếu bạn có mẫu JSON login khác.
                var payload = new ApiWrapper<LoginCredentials>(credentials);

                var response = await _httpClient.PostAsJsonAsync("/api/guest/login", payload);

                if (response.IsSuccessStatusCode)
                {
                    var resultWrapper = await response.Content.ReadFromJsonAsync<ApiWrapper<LoginResult>>();

                    if (resultWrapper?.Value != null && !string.IsNullOrEmpty(resultWrapper.Value.Token))
                    {
                        _authToken = resultWrapper.Value.Token;
                        _currentRole = resultWrapper.Value.Role;

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

        public async Task<List<DriverProfile>> GetProfilesAsync(int limit = 1)
        {
            if (!IsLoggedIn) return new List<DriverProfile>();

            try
            {
                string role = string.IsNullOrEmpty(_currentRole) ? "staff" : _currentRole;

                // [CẬP NHẬT] Endpoint /xin là POST và cần body token + value: "1"
                var payload = new
                {
                    token = _authToken,
                    value = new { value = limit.ToString() }
                };

                var response = await _httpClient.PostAsJsonAsync($"/api/{role}/xin", payload);

                if (response.IsSuccessStatusCode)
                {
                    // Response: { "value": [ { "_id": "...", ... } ] }
                    var wrapper = await response.Content.ReadFromJsonAsync<ApiWrapper<List<DriverProfileDto>>>();

                    if (wrapper?.Value != null)
                    {
                        return wrapper.Value.Select(dto => new DriverProfile
                        {
                            FullName = dto.Name,
                            Cccd = dto.Cccd,
                            IssueDate = dto.NgayCap,
                            PhoneNumber = dto.SoDT,
                            Address = dto.Dctt,
                            WardCommune = dto.SoNha,
                            EngineNumber = dto.SoMay,
                            ChassisNumber = dto.SoKhung,
                            LicensePlate = dto.Id, // _id chính là LicensePlate
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
            // Hàm này dùng cho ImportViewModel, gửi lên /nhap
            if (!IsLoggedIn) return false;
            // Nếu không có LicensePlate (ID) thì không gửi được theo format mới
            if (string.IsNullOrEmpty(profile.LicensePlate)) return false;

            return await ConfirmImportedAsync(profile.LicensePlate);
        }

        public async Task<bool> ConfirmImportedAsync(string licensePlate)
        {
            if (!IsLoggedIn) return false;
            if (string.IsNullOrEmpty(licensePlate)) return false;

            try
            {
                string role = string.IsNullOrEmpty(_currentRole) ? "staff" : _currentRole;

                // [CẬP NHẬT] Endpoint /nhap là POST và cần body token + value: { _id: ... }
                var payload = new
                {
                    token = _authToken,
                    value = new { _id = licensePlate }
                };

                var response = await _httpClient.PostAsJsonAsync($"/api/{role}/nhap", payload);

                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Confirm Import Error: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                // Cách đơn giản nhất: Gửi 1 request nhẹ (HEAD) tới Base URL
                // Nếu Server phản hồi (dù là lỗi 401/404...) thì tức là có mạng
                using var request = new HttpRequestMessage(HttpMethod.Head, "");

                // Timeout ngắn (2 giây) để phát hiện mất mạng nhanh
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

                var response = await _httpClient.SendAsync(request, cts.Token);

                // Chỉ cần không ném Exception là có mạng
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}