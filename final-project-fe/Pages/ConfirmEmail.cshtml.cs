using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Net.Http;

namespace final_project_fe.Pages
{
    public class ConfirmEmailModel : PageModel
    {
        private readonly ILogger<ConfirmEmailModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public bool IsSuccess { get; set; }
        public bool IsLoading { get; set; } = true;

        public ConfirmEmailModel(
            ILogger<ConfirmEmailModel> logger,
            IOptions<ApiSettings> apiSettings,
            HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public async Task<IActionResult> OnGetAsync(string? userId)
        {
            if (string.IsNullOrEmpty(userId))
            {
                IsLoading = false;
                IsSuccess = false;
                return Page();
            }

            try
            {
                var apiUrl = $"{_apiSettings.BaseUrl}/Auth/ConfirmUser?UserId={userId}";

                var content = new StringContent("", System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(apiUrl, content);

                IsSuccess = response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xác nhận email.");
                IsSuccess = false;
            }

            IsLoading = false;
            return Page();
        }
    }
}
