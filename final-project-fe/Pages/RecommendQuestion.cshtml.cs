using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace final_project_fe.Pages
{
    public class RecommendQuestionModel : PageModel
    {
        private readonly ILogger<RecommendQuestionModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public RecommendQuestionModel(ILogger<RecommendQuestionModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        [BindProperty]
        public UpdateUserMetadataDto UserMetadata { get; set; } = new();
        public string CurrentUserId { get; private set; } = string.Empty;

        public bool IsSubmitted { get; set; } = false;
        public string Message { get; set; } = "";

        public async Task<IActionResult> OnPostAsync()
        {
            string? token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            CurrentUserId = jsonToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

            if (!Guid.TryParse(CurrentUserId, out Guid userId))
            {
                Message = "Không thể xác định người dùng từ token.";
                return Page();
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var json = JsonSerializer.Serialize(UserMetadata, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                string apiUrl = $"{_apiSettings.BaseUrl}/User/update/{userId}";
                var response = await _httpClient.PutAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    IsSubmitted = true;
                    Message = "Cập nhật hồ sơ học tập thành công!";
                }
                else
                {
                    Message = $"Lỗi: {await response.Content.ReadAsStringAsync()}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API UpdateMetadata: {ex.Message}");
                Message = "Có lỗi xảy ra khi gọi API.";
            }

            return Page();
        }

    }
}
