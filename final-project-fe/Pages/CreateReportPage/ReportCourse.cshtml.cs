using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Notification;
using final_project_fe.Dtos.Report;
using final_project_fe.Dtos.Users;
using final_project_fe.Pages.Mentor.MentorPage;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using System.Xml.Linq;

namespace final_project_fe.Pages.CreateReportPage
{
    public class ReportCourseModel : PageModel
    {
        private readonly ILogger<ReportCourseModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public ReportCourseModel(ILogger<ReportCourseModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        [BindProperty]
        public ReportCourseDto ReportCourse { get; set; } = new();
        public string CurrentUserId { get; set; }
        public string BaseUrl { get; set; }

        public async Task<IActionResult> OnPostAsync(int CourseId, string Content)
        {
            BaseUrl = _apiSettings.BaseUrl;

            //Lưu trang trước đấy
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
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }

                try
                {
                    _httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    ReportCourse = new ReportCourseDto
                    {
                        CourseId = CourseId,
                        UserId = Guid.Parse(CurrentUserId),
                        Content = Content
                    };

                    var content = new StringContent(JsonSerializer.Serialize(ReportCourse), System.Text.Encoding.UTF8, "application/json");

                    string notiApiUrl = $"{BaseUrl}/ReportCourse";
                    var notiResponse = await _httpClient.PostAsync(notiApiUrl, content);

                    if (!notiResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Gửi thông báo thất bại: {notiResponse.StatusCode}");
                    }

                    TempData["SuccessMessage"] = $"Alert sent successfully.";
                    return GetPreviousPage();
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Lỗi API khi xóa báo cáo: {ex.Message}");
                    TempData["ErrorMessage"] = "Server error.";
                    return GetPreviousPage();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi không xác định: {ex.Message}");
                TempData["ErrorMessage"] = "Unexpected error.";
                return GetPreviousPage();
            }
        }

        private IActionResult GetPreviousPage()
        {
            const string sessionKey = "PageHistory";
            var history = HttpContext.Session.GetString(sessionKey);
            if (!string.IsNullOrEmpty(history))
            {
                var pageHistory = JsonSerializer.Deserialize<List<string>>(history);

                if (pageHistory.Count > 1)
                {
                    // Xóa trang hiện tại
                    pageHistory.RemoveAt(pageHistory.Count - 1);
                    var previousPage = pageHistory.Last();

                    // Lưu lại session sau khi back
                    HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(pageHistory));

                    return Redirect(previousPage);
                }
            }

            return RedirectToPage("/Index");
        }
    }
}
