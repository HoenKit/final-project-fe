using final_project_fe.Dtos.Transaction;
using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Payment;
using final_project_fe.Dtos.Withdraw;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using final_project_fe.Dtos.Mentors;
using System.Buffers.Text;
using System.Text.Json;
using System.Reflection.Emit;
using System.Web;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class DashboardModel : PageModel
    {
        private readonly ILogger<DashboardModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        public DashboardModel(ILogger<DashboardModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public string CurrentUserId { get; set; }
        public List<string> UserRoles { get; private set; } = new List<string>();
        public GetMentorDto? CurrentMentor { get; set; }
        public string BaseUrl { get; set; } = string.Empty;

        public decimal TotalIncome { get; set; }
        public int TotalCourse { get; set; }
        public int TotalStudent { get; set; }
        public PageResult<WithdrawDto> Withdraw { get; set; }

        public List<int> Courses { get; set; } = new();
        public List<decimal> Payments { get; set; } = new();
        public async Task<IActionResult> OnGetAsync()
        {
            if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Please login to access this page.";
                return RedirectToPage("/Login");
            }

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            if (jsonToken != null)
            {
                CurrentUserId = jsonToken.Claims
                    .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

                UserRoles = jsonToken.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();
            }

            if (UserRoles == null || !UserRoles.Contains("Mentor"))
            {
                TempData["ErrorMessage"] = "Access denied. You must be a mentor to view this page.";
                return RedirectToPage("/Index");
            }

            BaseUrl = _apiSettings.BaseUrl;

            // Get current mentor info
            var mentorResponse = await _httpClient.GetAsync($"{BaseUrl}/Mentor/get-by-user/{CurrentUserId}");
            if (!mentorResponse.IsSuccessStatusCode)
            {
                _logger.LogError("Cannot get mentor information for user: {UserId}", CurrentUserId);
                TempData["ErrorMessage"] = "Mentor information not found.";
                return RedirectToPage("/Index");
            }

            var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
            CurrentMentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (CurrentMentor == null)
            {
                TempData["ErrorMessage"] = "Mentor profile not found.";
                return RedirectToPage("/Index");
            }

            await LoadDashboardData();
            return Page();
        }

        private async Task LoadDashboardData()
        {
            if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
            {
                _logger.LogError("Access token not found");
                return;
            }

            await LoadCourseStats(token);
            await LoadWithdrawalData(token);
        }

        private async Task LoadCourseStats(string token)
        {
            try
            {
                var monthlyStatsUrl = $"{BaseUrl}/Course/monthly-stats/{CurrentUserId}";
                var monthlyStatsRequest = new HttpRequestMessage(HttpMethod.Get, monthlyStatsUrl);
                monthlyStatsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var monthlyStatsResponse = await _httpClient.SendAsync(monthlyStatsRequest);
                if (!monthlyStatsResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Failed to get monthly stats. Status: {StatusCode}", monthlyStatsResponse.StatusCode);
                    return;
                }

                var json = await monthlyStatsResponse.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var stats = JsonSerializer.Deserialize<List<MonthlyStatCourseDto>>(json, options)
                            ?? new List<MonthlyStatCourseDto>();

                ProcessMonthlyStats(stats);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading course statistics");
            }
        }

        private void ProcessMonthlyStats(List<MonthlyStatCourseDto> stats)
        {
            var currentDate = DateTime.Now;
            var currentMonthKey = currentDate.ToString("MM/yyyy");

            // Tìm thống kê tháng hiện tại
            var currentMonthStat = stats.FirstOrDefault(s => s.Time == currentMonthKey);

            // Cập nhật giá trị tháng hiện tại
            TotalCourse = currentMonthStat?.TotalCoursesCreated ?? 0;
            TotalStudent = currentMonthStat?.TotalStudentsEnrolled ?? 0;
            TotalIncome = currentMonthStat?.TotalEarnings ?? 0;

            // Tạo dữ liệu đầy đủ cho tất cả các tháng
            PrepareFullMonthData(stats, currentDate);
        }

        private void PrepareFullMonthData(List<MonthlyStatCourseDto> stats, DateTime currentDate)
        {
            Courses.Clear();
            Payments.Clear();

            // Tạo dictionary để tra cứu nhanh
            var statsDict = stats.ToDictionary(s => s.Time);

            // Lấy năm hiện tại và tháng hiện tại
            int currentYear = currentDate.Year;
            int currentMonth = currentDate.Month;

            // Duyệt qua tất cả các tháng từ tháng 1 đến tháng hiện tại
            for (int month = 1; month <= currentMonth; month++)
            {
                var monthKey = $"{month:D2}/{currentYear}"; // Định dạng "MM/yyyy"

                // Thêm giá trị, mặc định 0 nếu không có dữ liệu
                Courses.Add(statsDict.TryGetValue(monthKey, out var stat) ? stat.TotalStudentsEnrolled : 0);
                Payments.Add(statsDict.TryGetValue(monthKey, out stat) ? stat.TotalEarnings : 0);
            }

        }

        // Phương thức placeholder cho phần bạn muốn tự triển khai sau
        private async Task LoadWithdrawalData(string token)
        {
            try
            {
                var withdrawalUrl = new UriBuilder($"{BaseUrl}/Withdraw");
                var withdrawalQuery = HttpUtility.ParseQueryString(string.Empty);

                withdrawalQuery["page"] = "1";
                withdrawalQuery["pageSize"] = "1000";
                withdrawalQuery["sortOption"] = "desc_date";
                withdrawalQuery["mentorId"] = CurrentMentor.MentorId.ToString();
                withdrawalQuery["status"] = "Accepted";
                withdrawalQuery["isCurrentMonth"] = "True";

                withdrawalUrl.Query = withdrawalQuery.ToString();

                var withdrawRequest = new HttpRequestMessage(HttpMethod.Get, withdrawalUrl.ToString());
                withdrawRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var withdrawResponse = await _httpClient.SendAsync(withdrawRequest);
                if (withdrawResponse.IsSuccessStatusCode)
                {
                    var withdrawJson = await withdrawResponse.Content.ReadAsStringAsync();
                    Withdraw = JsonSerializer.Deserialize<PageResult<WithdrawDto>>(withdrawJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new PageResult<WithdrawDto>(new List<WithdrawDto>(), 0, 1, 1000);
                }
                else
                {
                    Withdraw = new PageResult<WithdrawDto>(new List<WithdrawDto>(), 0, 1, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading withdraws");
                Withdraw = new PageResult<WithdrawDto>(new List<WithdrawDto>(), 0, 1, 0);
            }
        }
    }
}
