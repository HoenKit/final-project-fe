using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using final_project_fe.Dtos.Users;
using System.Net.Http.Headers;
using System.Text.Json;
using final_project_fe.Dtos.Post;

namespace final_project_fe.Pages.Admin.Dashboard
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

        // Add these properties to your model
        public List<string> ChartLabels { get; set; } = new();
        public List<int> MembershipsData { get; set; }
        public List<int> ArticlesData { get; set; } = new();
        public List<int> UsersData { get; set; } = new();
        public string SalesPeriod { get; set; }
        public decimal TotalSales { get; set; }
        public List<decimal> DailySalesData { get; set; }
        public List<string> DailySalesLabels { get; set; }
        public int TotalUsers { get; set; }
        public int TotalArticles { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            if (role != "Admin")
                return RedirectToPage("/Index");

            //Gọi API thống kê user theo tháng
            var Userurl = $"{_apiSettings.BaseUrl}/User/monthly-stats";
            var userRequest = new HttpRequestMessage(HttpMethod.Get, Userurl);
            userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var userResponse = await _httpClient.SendAsync(userRequest);
                if (userResponse.IsSuccessStatusCode)
                {
                    var json = await userResponse.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var stats = JsonSerializer.Deserialize<List<MonthlyStatDto>>(json, options)
                                ?? new List<MonthlyStatDto>();

                    var statsDict = stats.ToDictionary(
                        s => s.Month,
                        s => s.Total
                    );

                    int currentYear = DateTime.Now.Year;
                    int currentMonth = DateTime.Now.Month;

                    for (int month = 1; month <= currentMonth; month++)
                    {
                        var key = month.ToString("D2") + "/" + currentYear;
                        ChartLabels.Add(new DateTime(currentYear, month, 1).ToString("MMMM"));
                        UsersData.Add(statsDict.ContainsKey(key) ? statsDict[key] : 0);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling monthly-stats of user API");
            }

            //Gọi API thống kê post theo tháng
            var Posturl = $"{_apiSettings.BaseUrl}/Post/monthly-stats";
            var postRequest = new HttpRequestMessage(HttpMethod.Get, Posturl);
            postRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var postResponse = await _httpClient.SendAsync(postRequest);
                if (postResponse.IsSuccessStatusCode)
                {
                    var json = await postResponse.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var stats = JsonSerializer.Deserialize<List<MonthlyStatDto>>(json, options)
                                ?? new List<MonthlyStatDto>();

                    var statsDict = stats.ToDictionary(
                        s => s.Month,
                        s => s.Total
                    );

                    int currentYear = DateTime.Now.Year;
                    int currentMonth = DateTime.Now.Month;

                    for (int month = 1; month <= currentMonth; month++)
                    {
                        var key = month.ToString("D2") + "/" + currentYear;
                        ChartLabels.Add(new DateTime(currentYear, month, 1).ToString("MMMM"));
                        ArticlesData.Add(statsDict.ContainsKey(key) ? statsDict[key] : 0);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling monthly-stats of post API");
            }

            //Lấy TotalCount từ get post
            var PostTotalCounturl = $"{_apiSettings.BaseUrl}/Post";
            var postTotalCounturlRequest = new HttpRequestMessage(HttpMethod.Get, PostTotalCounturl);
            postTotalCounturlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var postResponse = await _httpClient.SendAsync(postTotalCounturlRequest);
                if (postResponse.IsSuccessStatusCode)
                {
                    var json = await postResponse.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    var apiResponse = JsonSerializer.Deserialize<PostManagerDto>(json, options);

                    if (apiResponse != null)
                    {
                        TotalArticles = apiResponse.TotalCount;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling Post API");
            }

            //Lấy TotalCount từ get user
            var UserTotalCounturl = $"{_apiSettings.BaseUrl}/User";
            var userTotalCounturlRequest = new HttpRequestMessage(HttpMethod.Get, UserTotalCounturl);
            userTotalCounturlRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var userResponse = await _httpClient.SendAsync(userTotalCounturlRequest);
                if (userResponse.IsSuccessStatusCode)
                {
                    var json = await userResponse.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                    var apiResponse = JsonSerializer.Deserialize<User>(json, options);

                    if (apiResponse != null)
                    {
                        TotalUsers = apiResponse.TotalCount;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling User API");
            }

            // Daily Sales data
            SalesPeriod = $"{DateTime.Now.AddMonths(-1):MMMM dd} - {DateTime.Now:MMMM dd}";
            TotalSales = 4578.58m;

            // Example data - replace with your actual data source
            DailySalesData = new List<decimal> { 65, 59, 80, 81, 56, 55, 40, 35, 30 };
            DailySalesLabels = new List<string> { "Day 1", "Day 2", "Day 3", "Day 4", "Day 5", "Day 6", "Day 7", "Day 8", "Day 9" };

            // Example data - replace with your actual data source
            ChartLabels = new List<string> { "Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };

            MembershipsData = new List<int> { 154, 184, 175, 203, 210, 231, 240, 278, 252, 312, 320, 374 };
            //UsersData = new List<int> { 256, 230, 245, 287, 240, 250, 230, 295, 331, 431, 456, 521 };
            //ArticlesData = new List<int> { 542, 480, 430, 550, 530, 453, 380, 434, 568, 610, 700, 900 };

            // In a real app, you would get this data from a service/database
            return Page();
        }
    }
}
