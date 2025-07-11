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

namespace final_project_fe.Pages.Admin.ReportManager.PostsReport
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

        public PageResult<PostManagerDto> Posts { get; set; }
        public PageResult<GroupedReportDto<int, ReportPostDto>> GroupedReportPosts { get; set; }
        public Dictionary<int, List<ReportPostDto>> DetailedReports { get; set; } = new Dictionary<int, List<ReportPostDto>>();
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

            string groupedUrl = $"{_apiSettings.BaseUrl}/ReportPost/grouped?page={pageNumber}";
            string postApiUrl = $"{_apiSettings.BaseUrl}/Post/";

            try
            {
                var requestGrouped = new HttpRequestMessage(HttpMethod.Get, groupedUrl);
                requestGrouped.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestPost = new HttpRequestMessage(HttpMethod.Get, postApiUrl);
                requestPost.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi api song song 3 request
                var groupedTask = _httpClient.SendAsync(requestGrouped);
                var postTask = _httpClient.SendAsync(requestPost);

                await Task.WhenAll(groupedTask, postTask);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Xử lý kết quả lấy được
                if (groupedTask.Result.IsSuccessStatusCode)
                {
                    string json = await groupedTask.Result.Content.ReadAsStringAsync();
                    GroupedReportPosts = JsonSerializer.Deserialize<PageResult<GroupedReportDto<int, ReportPostDto>>>(json, options);
                }

                if (postTask.Result.IsSuccessStatusCode)
                {
                    string json = await postTask.Result.Content.ReadAsStringAsync();
                    Posts = JsonSerializer.Deserialize<PageResult<PostManagerDto>>(json, options);
                }

                if (GroupedReportPosts?.Items != null && GroupedReportPosts.Items.Any())
                {
                    await LoadDetailedReportsAsync(token, GroupedReportPosts.Items);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }

            GroupedReportPosts ??= new PageResult<GroupedReportDto<int,ReportPostDto>>(Enumerable.Empty<GroupedReportDto<int, ReportPostDto>>(), 0, CurrentPage, PageSize);
            Posts ??= new PageResult<PostManagerDto>(Enumerable.Empty<PostManagerDto>(), 0, CurrentPage, PageSize);

            return Page();
        }

        private async Task LoadDetailedReportsAsync(string token, IEnumerable<GroupedReportDto<int, ReportPostDto>> groupedReports)
        {
            var postIds = groupedReports.Select(g => g.Id).ToList();
            var detailTasks = new List<Task<KeyValuePair<int, List<ReportPostDto>>>>();

            // Tạo tasks để gọi API song song cho tất cả postIds
            foreach (var postId in postIds)
            {
                detailTasks.Add(GetReportDetailsByPostIdAsync(token, postId));
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
                DetailedReports = new Dictionary<int, List<ReportPostDto>>();
            }
        }

        private async Task<KeyValuePair<int, List<ReportPostDto>>> GetReportDetailsByPostIdAsync(string token, int postId)
        {
            string apiUrl = $"{_apiSettings.BaseUrl}/ReportManager/by-post/{postId}";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var reports = JsonSerializer.Deserialize<List<ReportPostDto>>(json, options) ?? new List<ReportPostDto>();

                    return new KeyValuePair<int, List<ReportPostDto>>(postId, reports);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API chi tiết report cho post {postId}: {ex.Message}");
            }

            return new KeyValuePair<int, List<ReportPostDto>>(postId, new List<ReportPostDto>());
        }

        public async Task<IActionResult> OnPostDeleteReportAsync(Guid id, string title)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var notification = new CreateNotification
                {
                    userId = id,
                    message = $"Warning users whose articles titled '{title}' have been reported as not following our site's community standards."
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
