using final_project_fe.Dtos.Report;
using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace final_project_fe.Pages.Admin.ReportManager.PostsReport
{
    public class DeleteReportModel : PageModel
    {
        private readonly ILogger<DeleteReportModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public DeleteReportModel(ILogger<DeleteReportModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public GroupedReportDto<int, ReportPostDto> GroupedReportPosts { get; set; }

        public async Task<IActionResult> OnPostDeleteReportAsync(int id)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            if (role != "Admin")
                return RedirectToPage("/Index");

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string apiUrl = $"{_apiSettings.BaseUrl}/ReportPost/DeleteReportsByPostId/{id}";

                var response = await _httpClient.DeleteAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Report deleted successfully.";
                    return RedirectToPage("/Admin/ReportManager/PostsReport/Index");
                }
                else
                {
                    _logger.LogError($"Delete failed report: {response.StatusCode}");
                    TempData["ErrorMessage"] = "Delete report failed.";
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"API error when deleting report: {ex.Message}");
                TempData["ErrorMessage"] = "Error calling API.";
                return RedirectToPage();
            }
        }
    }
}
