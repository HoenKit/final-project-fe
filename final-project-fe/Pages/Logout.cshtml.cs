using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace final_project_fe.Pages
{
    public class LogoutModel : PageModel
    {
        private readonly ILogger<LogoutModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        public LogoutModel(ILogger<LogoutModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
            {
                return RedirectToPage("/Index");
            }

            string logoutApiUrl = $"{_apiSettings.BaseUrl}/Auth/Logout";
            string token = Request.Cookies["AccessToken"];

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage response = await _httpClient.PostAsync(logoutApiUrl, null);

                _logger.LogInformation($"Logout API Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    Response.Cookies.Delete("AccessToken");
                    _logger.LogInformation("User logged out successfully.");
                    TempData["SuccessMessage"] = "Logout successful!";
                    ViewData["ResetLoadingSession"] = true;
                    return RedirectToPage("/Index");
                }
                else
                {
                    _logger.LogWarning("Logout API failed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API logout: {ex.Message}");
            }

            return RedirectToPage("/ErrorPage");
        }


    }
}
