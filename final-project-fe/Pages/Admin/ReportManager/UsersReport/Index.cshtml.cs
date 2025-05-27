using final_project_fe.Dtos.Report;
using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;
using final_project_fe.Dtos.Users;

namespace final_project_fe.Pages.Admin.ReportManager.UsersReport
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

        public PageResult<User> Users { get; set; }
        public PageResult<GroupedReportDto<Guid, ReportUserDto>> GroupedReportUsers { get; set; }
        public Dictionary<Guid, List<ReportUserDto>> DetailedReports { get; set; } = new Dictionary<Guid, List<ReportUserDto>>();
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

            string groupedUrl = $"{_apiSettings.BaseUrl}/ReportUser/grouped?page={pageNumber}";
            string userApiUrl = $"{_apiSettings.BaseUrl}/User/";

            try
            {
                var requestGrouped = new HttpRequestMessage(HttpMethod.Get, groupedUrl);
                requestGrouped.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestUser = new HttpRequestMessage(HttpMethod.Get, userApiUrl);
                requestUser.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi api song song 2 request
                var groupedTask = _httpClient.SendAsync(requestGrouped);
                var userTask = _httpClient.SendAsync(requestUser);

                await Task.WhenAll(groupedTask, userTask);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Xử lý kết quả lấy được
                if (groupedTask.Result.IsSuccessStatusCode)
                {
                    string json = await groupedTask.Result.Content.ReadAsStringAsync();
                    GroupedReportUsers = JsonSerializer.Deserialize<PageResult<GroupedReportDto<Guid, ReportUserDto>>>(json, options);
                }

                if (userTask.Result.IsSuccessStatusCode)
                {
                    string json = await userTask.Result.Content.ReadAsStringAsync();
                    Users = JsonSerializer.Deserialize<PageResult<User>>(json, options);
                }

                if (GroupedReportUsers?.Items != null && GroupedReportUsers.Items.Any())
                {
                    await LoadDetailedReportsAsync(token, GroupedReportUsers.Items);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }

            GroupedReportUsers ??= new PageResult<GroupedReportDto<Guid, ReportUserDto>>(Enumerable.Empty<GroupedReportDto<Guid, ReportUserDto>>(), 0, CurrentPage, PageSize);
            Users ??= new PageResult<User>(Enumerable.Empty<User>(), 0, CurrentPage, PageSize);

            return Page();
        }

        private async Task LoadDetailedReportsAsync(string token, IEnumerable<GroupedReportDto<Guid, ReportUserDto>> groupedReports)
        {
            var userIds = groupedReports.Select(g => g.Id).ToList();
            var detailTasks = new List<Task<KeyValuePair<Guid, List<ReportUserDto>>>>();

            // Tạo tasks để gọi API song song cho tất cả userIds
            foreach (var userId in userIds)
            {
                detailTasks.Add(GetReportDetailsByUserIdAsync(token, userId));
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
                DetailedReports = new Dictionary<Guid, List<ReportUserDto>>();
            }
        }

        private async Task<KeyValuePair<Guid, List<ReportUserDto>>> GetReportDetailsByUserIdAsync(string token, Guid userId)
        {
            string apiUrl = $"{_apiSettings.BaseUrl}/ReportManager/by-user/{userId}";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var reports = JsonSerializer.Deserialize<List<ReportUserDto>>(json, options) ?? new List<ReportUserDto>();

                    return new KeyValuePair<Guid, List<ReportUserDto>>(userId, reports);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API chi tiết report cho user {userId}: {ex.Message}");
            }

            return new KeyValuePair<Guid, List<ReportUserDto>>(userId, new List<ReportUserDto>());
        }
    }
}