using final_project_fe.Dtos.Report;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace final_project_fe.Pages.Admin.ReportManager.CommentsReport
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

        public GroupedReportDto<int, ReportCommentDto> GroupedReportComments { get; set; }

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

                string apiUrl = $"{_apiSettings.BaseUrl}/ReportComment/DeleteReportsByCommentId/{id}";

                var response = await _httpClient.DeleteAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Delete report successful.";
                    return RedirectToPage("/Admin/ReportManager/CommentsReport/Index");
                }
                else
                {
                    _logger.LogError($"Xóa báo cáo thất bại: {response.StatusCode}");
                    TempData["ErrorMessage"] = "Delete report failed.";
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi API khi xóa báo cáo: {ex.Message}");
                TempData["ErrorMessage"] = "Server error.";
                return RedirectToPage();
            }
        }
    }
}
