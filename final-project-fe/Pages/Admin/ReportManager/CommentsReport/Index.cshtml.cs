using final_project_fe.Dtos.Report;
using final_project_fe.Dtos.Users;
using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;
using final_project_fe.Dtos.Comment;
using final_project_fe.Dtos.Notification;
using Microsoft.Extensions.Hosting;
using final_project_fe.Dtos.Post;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace final_project_fe.Pages.Admin.ReportManager.CommentsReport
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

        public PageResult<CommentDto> Comments { get; set; }
        public CommentDto Comment { get; set; }
        public PageResult<GroupedReportDto<int, ReportCommentDto>> GroupedReportComments { get; set; }
        public Dictionary<int, List<ReportCommentDto>> DetailedReports { get; set; } = new Dictionary<int, List<ReportCommentDto>>();
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

            string groupedUrl = $"{_apiSettings.BaseUrl}/ReportComment/grouped?page={pageNumber}";
            string commentApiUrl = $"{_apiSettings.BaseUrl}/Comment/GetAllComments";

            try
            {
                var requestGrouped = new HttpRequestMessage(HttpMethod.Get, groupedUrl);
                requestGrouped.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestComment = new HttpRequestMessage(HttpMethod.Get, commentApiUrl);
                requestComment.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi api song song 2 request
                var groupedTask = _httpClient.SendAsync(requestGrouped);
                var commentTask = _httpClient.SendAsync(requestComment);

                await Task.WhenAll(groupedTask, commentTask);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Xử lý kết quả lấy được
                if (groupedTask.Result.IsSuccessStatusCode)
                {
                    string json = await groupedTask.Result.Content.ReadAsStringAsync();
                    GroupedReportComments = JsonSerializer.Deserialize<PageResult<GroupedReportDto<int, ReportCommentDto>>>(json, options);
                }

                if (commentTask.Result.IsSuccessStatusCode)
                {
                    string json = await commentTask.Result.Content.ReadAsStringAsync();
                    Comments = JsonSerializer.Deserialize<PageResult<CommentDto>>(json, options);
                }

                if (GroupedReportComments?.Items != null && GroupedReportComments.Items.Any())
                {
                    await LoadDetailedReportsAsync(token, GroupedReportComments.Items);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }

            GroupedReportComments ??= new PageResult<GroupedReportDto<int, ReportCommentDto>>(Enumerable.Empty<GroupedReportDto<int, ReportCommentDto>>(), 0, CurrentPage, PageSize);
            Comments ??= new PageResult<CommentDto>(Enumerable.Empty<CommentDto>(), 0, CurrentPage, PageSize);

            return Page();
        }

        private async Task LoadDetailedReportsAsync(string token, IEnumerable<GroupedReportDto<int, ReportCommentDto>> groupedReports)
        {
            var commentIds = groupedReports.Select(g => g.Id).ToList();
            var detailTasks = new List<Task<KeyValuePair<int, List<ReportCommentDto>>>>();

            // Tạo tasks để gọi API song song cho tất cả userIds
            foreach (var commentId in commentIds)
            {
                detailTasks.Add(GetReportDetailsByUserIdAsync(token, commentId));
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
                DetailedReports = new Dictionary<int, List<ReportCommentDto>>();
            }
        }

        private async Task<KeyValuePair<int, List<ReportCommentDto>>> GetReportDetailsByUserIdAsync(string token, int commentId)
        {
            string apiUrl = $"{_apiSettings.BaseUrl}/ReportManager/by-comment/{commentId}";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var reports = JsonSerializer.Deserialize<List<ReportCommentDto>>(json, options) ?? new List<ReportCommentDto>();

                    return new KeyValuePair<int, List<ReportCommentDto>>(commentId, reports);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API chi tiết report cho user {commentId}: {ex.Message}");
            }

            return new KeyValuePair<int, List<ReportCommentDto>>(commentId, new List<ReportCommentDto>());
        }

        public async Task<IActionResult> OnPostDeleteReportAsync(int id)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);


                string commentUrl = $"{_apiSettings.BaseUrl}/Comment/{id}";
                var commentRequest = new HttpRequestMessage(HttpMethod.Get, commentUrl);
                commentRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var commentResponse = await _httpClient.SendAsync(commentRequest);
                if (!commentResponse.IsSuccessStatusCode)
                {
                    return RedirectToPage("/Page/ErrorPage");
                }

                var commentJson = await commentResponse.Content.ReadAsStringAsync();
                var commentRoot = JsonNode.Parse(commentJson);
                Comment = commentRoot.Deserialize<CommentDto>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new CommentDto();

                var notification = new CreateNotification
                {
                    userId = Comment.UserId,
                    message = $"Warn users whose comments are reported as not following our site's community standards."
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
