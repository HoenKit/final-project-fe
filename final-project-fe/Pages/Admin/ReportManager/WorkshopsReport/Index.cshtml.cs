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
using final_project_fe.Dtos.WorkShop;
using final_project_fe.Dtos.Mentors;
using System.Text.Json.Nodes;

namespace final_project_fe.Pages.Admin.ReportManager.WorkshopsReport
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

        public PageResult<WorkShopDto> WorkShops { get; set; }
        public PageResult<GroupedReportDto<int, ReportWorkShopDto>> GroupedReportWorkShops { get; set; }
        public Dictionary<int, List<ReportWorkShopDto>> DetailedReports { get; set; } = new Dictionary<int, List<ReportWorkShopDto>>();
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

            string groupedUrl = $"{_apiSettings.BaseUrl}/ReportWorkShop/grouped?page={pageNumber}";
            string workShopsApiUrl = $"{_apiSettings.BaseUrl}/WorkShop?page=1&pageSize=1000";

            try
            {
                var requestGrouped = new HttpRequestMessage(HttpMethod.Get, groupedUrl);
                requestGrouped.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestWorkShop = new HttpRequestMessage(HttpMethod.Get, workShopsApiUrl);
                requestWorkShop.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi api song song 3 request
                var groupedTask = _httpClient.SendAsync(requestGrouped);
                var workShopTask = _httpClient.SendAsync(requestWorkShop);

                await Task.WhenAll(groupedTask, workShopTask);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Xử lý kết quả lấy được
                if (groupedTask.Result.IsSuccessStatusCode)
                {
                    string json = await groupedTask.Result.Content.ReadAsStringAsync();
                    GroupedReportWorkShops = JsonSerializer.Deserialize<PageResult<GroupedReportDto<int, ReportWorkShopDto>>>(json, options);
                }

                if (workShopTask.Result.IsSuccessStatusCode)
                {
                    string json = await workShopTask.Result.Content.ReadAsStringAsync();
                    WorkShops = JsonSerializer.Deserialize<PageResult<WorkShopDto>>(json, options);
                }

                if (GroupedReportWorkShops?.Items != null && GroupedReportWorkShops.Items.Any())
                {
                    await LoadDetailedReportsAsync(token, GroupedReportWorkShops.Items);
                }

            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }

            GroupedReportWorkShops ??= new PageResult<GroupedReportDto<int, ReportWorkShopDto>>(Enumerable.Empty<GroupedReportDto<int, ReportWorkShopDto>>(), 0, CurrentPage, PageSize);
            WorkShops ??= new PageResult<WorkShopDto>(Enumerable.Empty<WorkShopDto>(), 0, CurrentPage, PageSize);

            return Page();
        }

        private async Task LoadDetailedReportsAsync(string token, IEnumerable<GroupedReportDto<int, ReportWorkShopDto>> groupedReports)
        {
            var postIds = groupedReports.Select(g => g.Id).ToList();
            var detailTasks = new List<Task<KeyValuePair<int, List<ReportWorkShopDto>>>>();

            // Tạo tasks để gọi API song song cho tất cả postIds
            foreach (var workShopId in postIds)
            {
                detailTasks.Add(GetReportDetailsByWorkShopIdAsync(token, workShopId));
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
                DetailedReports = new Dictionary<int, List<ReportWorkShopDto>>();
            }
        }

        private async Task<KeyValuePair<int, List<ReportWorkShopDto>>> GetReportDetailsByWorkShopIdAsync(string token, int workShopId)
        {
            string apiUrl = $"{_apiSettings.BaseUrl}/ReportManager/by-WorkShop/{workShopId}";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var reports = JsonSerializer.Deserialize<List<ReportWorkShopDto>>(json, options) ?? new List<ReportWorkShopDto>();

                    return new KeyValuePair<int, List<ReportWorkShopDto>>(workShopId, reports);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API chi tiết report cho WorkShop {workShopId}: {ex.Message}");
            }

            return new KeyValuePair<int, List<ReportWorkShopDto>>(workShopId, new List<ReportWorkShopDto>());
        }

        public async Task<IActionResult> OnPostWarningAsync(int workShopId, string decription)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string workShopUrl = $"{_apiSettings.BaseUrl}/WorkShop/{workShopId}";
                var workShopRequest = new HttpRequestMessage(HttpMethod.Get, workShopUrl);
                workShopRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var workShopResponse = await _httpClient.SendAsync(workShopRequest);
                if (!workShopResponse.IsSuccessStatusCode)
                {
                    return RedirectToPage("/Page/ErrorPage");
                }

                var workShopJson = await workShopResponse.Content.ReadAsStringAsync();
                var workShopRoot = JsonNode.Parse(workShopJson);
                var WorkShop = workShopRoot.Deserialize<WorkShopDto>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new WorkShopDto();

                string mentorUrl = $"{_apiSettings.BaseUrl}/Mentor/{WorkShop.MentorId}";
                var mentorRequest = new HttpRequestMessage(HttpMethod.Get, mentorUrl);
                mentorRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var mentorResponse = await _httpClient.SendAsync(mentorRequest);
                if (!mentorResponse.IsSuccessStatusCode)
                {
                    return RedirectToPage("/Page/ErrorPage");
                }

                var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                var mentorRoot = JsonNode.Parse(mentorJson);
                var Mentor = mentorRoot.Deserialize<MentorDto>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new MentorDto();

                var notification = new CreateNotification
                {
                    userId = Mentor.UserId,
                    message = $"Warning users whose WorkShop with name '{decription}' have been reported as not following our site's community standards."
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
