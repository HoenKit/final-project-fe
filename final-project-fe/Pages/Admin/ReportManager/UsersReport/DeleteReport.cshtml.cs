using final_project_fe.Dtos.Report;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace final_project_fe.Pages.Admin.ReportManager.UsersReport
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

        public GroupedReportDto<Guid, ReportUserDto> GroupedReportUsers { get; set; }

        public async Task<IActionResult> OnPostDeleteReportAsync(Guid id)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            if (role != "Admin")
                return RedirectToPage("/Index");

            //Lấy trang trước đấy
            const string sessionKey = "PageHistory";
            var history = HttpContext.Session.GetString(sessionKey);
            List<string> pageHistory;

            if (string.IsNullOrEmpty(history))
            {
                pageHistory = new List<string>();
            }
            else
            {
                pageHistory = JsonSerializer.Deserialize<List<string>>(history);
            }

            // Lấy URL hiện tại
            var currentUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;

            // Chỉ thêm nếu khác trang cuối cùng
            if (pageHistory.Count == 0 || pageHistory.Last() != currentUrl)
            {
                pageHistory.Add(currentUrl);
            }

            // Lưu lại vào session
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(pageHistory));

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string apiUrl = $"{_apiSettings.BaseUrl}/ReportUser/DeleteReportsByUserId/{id}";

                var response = await _httpClient.DeleteAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Xóa báo cáo thành công.";
                    return RedirectToPage("/Admin/ReportManager/UsersReport/Index");
                }
                else
                {
                    _logger.LogError($"Xóa báo cáo thất bại: {response.StatusCode}");
                    TempData["ErrorMessage"] = "Xóa báo cáo thất bại.";
                    return RedirectToPage();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi API khi xóa báo cáo: {ex.Message}");
                TempData["ErrorMessage"] = "Lỗi khi gọi API.";
                return RedirectToPage();
            }
        }
    }
}
