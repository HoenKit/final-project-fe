using final_project_fe.Dtos.Post;
using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;
using final_project_fe.Dtos.Report;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Users;
using final_project_fe.Dtos.Notification;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Comment;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using final_project_fe.Dtos.Mentors;

namespace final_project_fe.Pages.Admin.ReportManager.CoursesReport
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public IndexModel(ILogger<IndexModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public PageResult<GetCourseDto> Courses { get; set; }
        public MentorDto Mentor { get; set; }
        public PageResult<GroupedReportDto<int, ReportCourseDto>> GroupedReportCourses { get; set; }
        public Dictionary<int, List<ReportCourseDto>> DetailedReports { get; set; } = new Dictionary<int, List<ReportCourseDto>>();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; } = 10;
        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
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

            CurrentPage = pageNumber;

            string groupedUrl = $"{_apiSettings.BaseUrl}/ReportCourse/grouped?page={pageNumber}";
            string courseApiUrl = $"{_apiSettings.BaseUrl}/Course/";

            try
            {
                var requestGrouped = new HttpRequestMessage(HttpMethod.Get, groupedUrl);
                requestGrouped.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestCourse = new HttpRequestMessage(HttpMethod.Get, courseApiUrl);
                requestCourse.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi api song song 2 request
                var groupedTask = _httpClient.SendAsync(requestGrouped);
                var courseTask = _httpClient.SendAsync(requestCourse);

                await Task.WhenAll(groupedTask, courseTask);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Xử lý kết quả lấy được
                if (groupedTask.Result.IsSuccessStatusCode)
                {
                    string json = await groupedTask.Result.Content.ReadAsStringAsync();
                    GroupedReportCourses = JsonSerializer.Deserialize<PageResult<GroupedReportDto<int, ReportCourseDto>>>(json, options);
                }

                if (courseTask.Result.IsSuccessStatusCode)
                {
                    string json = await courseTask.Result.Content.ReadAsStringAsync();
                    Courses = JsonSerializer.Deserialize<PageResult<GetCourseDto>>(json, options);
                }

                if (GroupedReportCourses?.Items != null && GroupedReportCourses.Items.Any())
                {
                    await LoadDetailedReportsAsync(token, GroupedReportCourses.Items);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }

            GroupedReportCourses ??= new PageResult<GroupedReportDto<int, ReportCourseDto>>(Enumerable.Empty<GroupedReportDto<int, ReportCourseDto>>(), 0, CurrentPage, PageSize);
            Courses ??= new PageResult<GetCourseDto>(Enumerable.Empty<GetCourseDto>(), 0, CurrentPage, PageSize);

            return Page();
        }

        private async Task LoadDetailedReportsAsync(string token, IEnumerable<GroupedReportDto<int, ReportCourseDto>> groupedReports)
        {
            var courseIds = groupedReports.Select(g => g.Id).ToList();
            var detailTasks = new List<Task<KeyValuePair<int, List<ReportCourseDto>>>>();

            // Tạo tasks để gọi API song song cho tất cả courseIds
            foreach (var courseId in courseIds)
            {
                detailTasks.Add(GetReportDetailsByCourseIdAsync(token, courseId));
            }

            try
            {
                // Chờ tất cả tasks hoàn thành
                var results = await Task.WhenAll(detailTasks);

                // Chuyển results thành Dictionary
                DetailedReports = results.ToDictionary(r => r.Key, r => r.Value);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy chi tiết reports: {ex.Message}");
                DetailedReports = new Dictionary<int, List<ReportCourseDto>>();
            }
        }

        private async Task<KeyValuePair<int, List<ReportCourseDto>>> GetReportDetailsByCourseIdAsync(string token, int courseId)
        {
            string apiUrl = $"{_apiSettings.BaseUrl}/ReportManager/by-Course/{courseId}";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var reports = JsonSerializer.Deserialize<List<ReportCourseDto>>(json, options) ?? new List<ReportCourseDto>();

                    return new KeyValuePair<int, List<ReportCourseDto>>(courseId, reports);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API chi tiết report cho course {courseId}: {ex.Message}");
            }

            return new KeyValuePair<int, List<ReportCourseDto>>(courseId, new List<ReportCourseDto>());
        }

        public async Task<IActionResult> OnPostWarningAsync(int courseId, string title)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string mentorUrl = $"{_apiSettings.BaseUrl}/Mentor/by-course/{courseId}";
                var mentorRequest = new HttpRequestMessage(HttpMethod.Get, mentorUrl);
                mentorRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var mentorResponse = await _httpClient.SendAsync(mentorRequest);
                if (!mentorResponse.IsSuccessStatusCode)
                {
                    return RedirectToPage("/Page/ErrorPage");
                }

                var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                var mentorRoot = JsonNode.Parse(mentorJson);
                Mentor = mentorRoot.Deserialize<MentorDto>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new MentorDto();

                var notification = new CreateNotification
                {
                    userId = Mentor.UserId,
                    message = $"Warning users whose Courses with name '{title}' have been reported as not following our site's community standards."
                };

                var content = new StringContent(JsonSerializer.Serialize(notification), System.Text.Encoding.UTF8, "application/json");

                string notiApiUrl = $"{_apiSettings.BaseUrl}/Notification";
                var notiResponse = await _httpClient.PostAsync(notiApiUrl, content);

                if (!notiResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"Gửi thông báo thất bại: {notiResponse.StatusCode}");
                }
                TempData["SuccessMessage"] = $"Alert sent successfully.";
                return RedirectToPage();
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
