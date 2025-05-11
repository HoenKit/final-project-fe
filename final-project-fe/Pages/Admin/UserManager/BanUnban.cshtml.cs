using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace final_project_fe.Pages.Admin.UserManager
{
    public class BanUnbanModel : PageModel
    {
        private readonly ILogger<BanUnbanModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public BanUnbanModel(ILogger<BanUnbanModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public User? User { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest("Invalid User ID");
            }

            string? token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            string? role = JwtHelper.GetRoleFromToken(token);
            _logger.LogInformation($"User Role: {role}");

            if (role != "Admin")
            {
                return RedirectToPage("/Index");
            }

            string apiUrl = $"{_apiSettings.BaseUrl}/User/toggle-ban/{userId}";

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage response = await _httpClient.PutAsync(apiUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    User = JsonSerializer.Deserialize<User>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return RedirectToPage("./Index");
                }
                else
                {
                    _logger.LogError($"Lỗi API khi cập nhật trạng thái user: {response.StatusCode}");
                    return StatusCode((int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}
