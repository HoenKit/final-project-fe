using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace final_project_fe.Pages.Admin.CourseManager
{
    public class CourseApprovalModel : PageModel
    {
        private readonly ILogger<CourseApprovalModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public CourseApprovalModel(
            ILogger<CourseApprovalModel> logger,
            IOptions<ApiSettings> apiSettings,
            HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public async Task<IActionResult> OnPostAsync(int id, string status)
        {
            string? token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            string? role = JwtHelper.GetRoleFromToken(token);
            if (role != "Admin")
                return RedirectToPage("/Index");

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string apiUrl = $"{_apiSettings.BaseUrl}/Course/toggle-status?id={id}&status={status}";

                HttpResponseMessage response = await _httpClient.PutAsync(apiUrl, null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = $"Course has been {status} successfully";
                    return RedirectToPage("./ListPending");
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to {status.ToLower()} course";
                    return RedirectToPage("./ListPending");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"API error when updating course status: {ex.Message}");
                return StatusCode(500);
            }
        }
    }
}