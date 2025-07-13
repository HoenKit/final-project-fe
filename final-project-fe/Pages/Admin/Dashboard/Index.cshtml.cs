using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using final_project_fe.Dtos.Users;
using System.Net.Http.Headers;
using System.Text.Json;
using final_project_fe.Dtos.Post;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Courses;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Web;
using final_project_fe.Dtos.Transaction;
using System.Buffers.Text;
using System.Reflection.Emit;
using final_project_fe.Dtos.Payment;

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

        // Chart and statistics properties
        public List<string> ChartLabels { get; set; } = new();
        public List<int> MembershipsData { get; set; } = new();
        public List<int> ArticlesData { get; set; } = new();
        public List<int> UsersData { get; set; } = new();

        // Dashboard summary properties
        public string CurrentUserId { get; set; }
        public int TotalUsers { get; set; }
        public int TotalArticles { get; set; }
        public int TotalMemberships { get; set; } = 576;
        public decimal TotalSales { get; set; }
        public decimal TotalSalesOneMonth { get; set; }

        // Sales chart properties
        public string SalesPeriod { get; set; }
        public List<decimal> DailySalesData { get; set; } = new();
        public List<string> DailySalesLabels { get; set; } = new();

        // Transaction properties
        public PageResult<GetTransactionDto> Transactions { get; set; }

        public async Task<IActionResult> OnGetAsync(string? status = null, string? email = null)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                CurrentUserId = jsonToken?.Claims
                                       .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            }

            if (role != "Admin")
                return RedirectToPage("/Index");

            // Load all dashboard data - simplified parameters
            await LoadDashboardData(token, status);

            return Page();
        }

        private async Task LoadDashboardData(string token, string? status)
        {
            // Load transactions for current month only (for display)
            await LoadTransactions(null, null, null, false, status, token);

            // Load user statistics
            await LoadUserStatistics(token);

            // Load article statistics
            await LoadArticleStatistics(token);

            // Load total counts
            await LoadTotalCounts(token);

            // Load sales data (this will also calculate TotalSales)
            await LoadSalesData(token);

            // Load chart data
            LoadChartData();
        }

        private async Task LoadTransactions(int? currentPage, Guid? userId, string? sortOption, bool filterByUser, string? status, string token)
        {
            try
            {
                var transactionUrl = new UriBuilder($"{_apiSettings.BaseUrl}/Transaction");
                var transactionQuery = HttpUtility.ParseQueryString(string.Empty);

                transactionQuery["page"] = "1";
                transactionQuery["pageSize"] = "1000";
                transactionQuery["sortOption"] = "desc_date";

                // Filter by single user
                if (userId.HasValue)
                    transactionQuery["userId"] = userId.Value.ToString();

                // Filter by user (only transactions of current user)
                if (filterByUser && !string.IsNullOrWhiteSpace(CurrentUserId))
                    transactionQuery["userId"] = CurrentUserId;

                // Status filter
                if (!string.IsNullOrWhiteSpace(status) && status != "all")
                {
                    var statusList = new List<string>();
                    if (status.Contains("Completed"))
                        statusList.Add("Completed");
                    if (status.Contains("Cancel"))
                        statusList.Add("Cancel");

                    foreach (var s in statusList)
                    {
                        transactionQuery.Add("statuses", s);
                    }
                }

                transactionUrl.Query = transactionQuery.ToString();

                var transactionRequest = new HttpRequestMessage(HttpMethod.Get, transactionUrl.ToString());
                transactionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var transactionResponse = await _httpClient.SendAsync(transactionRequest);
                if (transactionResponse.IsSuccessStatusCode)
                {
                    var transactionJson = await transactionResponse.Content.ReadAsStringAsync();
                    var allTransactions = JsonSerializer.Deserialize<PageResult<GetTransactionDto>>(transactionJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<GetTransactionDto>(new List<GetTransactionDto>(), 0, 1, 1000);

                    var currentMonth = DateTime.Now.Month;
                    var currentYear = DateTime.Now.Year;

                    var currentMonthTransactions = allTransactions.Items
                        .Where(t => t.CreateAt.Month == currentMonth && t.CreateAt.Year == currentYear)
                        .OrderByDescending(t => t.CreateAt)
                        .ToList();

                    Transactions = new PageResult<GetTransactionDto>(
                        currentMonthTransactions,
                        currentMonthTransactions.Count,
                        1,
                        currentMonthTransactions.Count
                    );
                }
                else
                {
                    Transactions = new PageResult<GetTransactionDto>(new List<GetTransactionDto>(), 0, 1, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading transactions for current month");
                Transactions = new PageResult<GetTransactionDto>(new List<GetTransactionDto>(), 0, 1, 0);
            }
        }

        // Separate method to load total sales from ALL transactions
        private async Task LoadTotalSales(string token)
        {
            try
            {
                var transactionUrl = new UriBuilder($"{_apiSettings.BaseUrl}/Transaction");
                var transactionQuery = HttpUtility.ParseQueryString(string.Empty);

                transactionQuery["page"] = "1";
                transactionQuery["pageSize"] = "10000";
                transactionQuery["sortOption"] = "desc_date";

                transactionUrl.Query = transactionQuery.ToString();

                var transactionRequest = new HttpRequestMessage(HttpMethod.Get, transactionUrl.ToString());
                transactionRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var transactionResponse = await _httpClient.SendAsync(transactionRequest);
                if (transactionResponse.IsSuccessStatusCode)
                {
                    var transactionJson = await transactionResponse.Content.ReadAsStringAsync();
                    var allTransactions = JsonSerializer.Deserialize<PageResult<GetTransactionDto>>(transactionJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<GetTransactionDto>(new List<GetTransactionDto>(), 0, 1, 10000);

                    TotalSales = allTransactions.Items
                        .Where(t => t.Status == "Completed" && t.Amount.HasValue)
                        .Sum(t => t.Amount.Value);

                    _logger.LogInformation($"Total sales calculated from {allTransactions.Items.Count(t => t.Status == "Completed")} completed transactions: {TotalSales:N0}₫");
                }
                else
                {
                    TotalSales = 0;
                    _logger.LogWarning("Failed to load transactions for total sales calculation");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading total sales from all transactions");
                TotalSales = 0;
            }
        }

        private async Task LoadUserStatistics(string token)
        {
            var userUrl = $"{_apiSettings.BaseUrl}/User/monthly-stats";
            var userRequest = new HttpRequestMessage(HttpMethod.Get, userUrl);
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

                    var statsDict = stats.ToDictionary(s => s.Month, s => s.Total);
                    int currentYear = DateTime.Now.Year;
                    int currentMonth = DateTime.Now.Month;

                    UsersData.Clear();
                    for (int month = 1; month <= currentMonth; month++)
                    {
                        var key = month.ToString("D2") + "/" + currentYear;
                        UsersData.Add(statsDict.ContainsKey(key) ? statsDict[key] : 0);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling monthly-stats of user API");
            }
        }

        private async Task LoadArticleStatistics(string token)
        {
            var postUrl = $"{_apiSettings.BaseUrl}/Post/monthly-stats";
            var postRequest = new HttpRequestMessage(HttpMethod.Get, postUrl);
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

                    var statsDict = stats.ToDictionary(s => s.Month, s => s.Total);
                    int currentYear = DateTime.Now.Year;
                    int currentMonth = DateTime.Now.Month;

                    ArticlesData.Clear();
                    for (int month = 1; month <= currentMonth; month++)
                    {
                        var key = month.ToString("D2") + "/" + currentYear;
                        ArticlesData.Add(statsDict.ContainsKey(key) ? statsDict[key] : 0);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling monthly-stats of post API");
            }
        }

        private async Task LoadTotalCounts(string token)
        {
            // Load total articles
            var postTotalCountUrl = $"{_apiSettings.BaseUrl}/Post";
            var postTotalCountRequest = new HttpRequestMessage(HttpMethod.Get, postTotalCountUrl);
            postTotalCountRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var postResponse = await _httpClient.SendAsync(postTotalCountRequest);
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

            // Load total users
            var userTotalCountUrl = $"{_apiSettings.BaseUrl}/User";
            var userTotalCountRequest = new HttpRequestMessage(HttpMethod.Get, userTotalCountUrl);
            userTotalCountRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var userResponse = await _httpClient.SendAsync(userTotalCountRequest);
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
        }

        private async Task LoadSalesData(string token)
        {
            try
            {
                // Get all payments for sales calculation
                var paymentUrl = new UriBuilder($"{_apiSettings.BaseUrl}/Payment");
                var paymentQuery = HttpUtility.ParseQueryString(string.Empty);

                paymentQuery["page"] = "1";
                paymentQuery["pageSize"] = "10000";
                paymentQuery["sortOption"] = "desc_date";

                paymentUrl.Query = paymentQuery.ToString();

                var paymentRequest = new HttpRequestMessage(HttpMethod.Get, paymentUrl.ToString());
                paymentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var paymentResponse = await _httpClient.SendAsync(paymentRequest);

                if (paymentResponse.IsSuccessStatusCode)
                {
                    var paymentJson = await paymentResponse.Content.ReadAsStringAsync();
                    var allPayments = JsonSerializer.Deserialize<PageResult<GetPaymentDto>>(paymentJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<GetPaymentDto>(new List<GetPaymentDto>(), 0, 1, 10000);

                    // Calculate TotalSales from ALL completed payments (all time)
                    TotalSales = allPayments.Items
                        .Where(p => p.Status == "Success")
                        .Sum(p => CalculateActualAmount(p));

                    // Calculate date range for Daily Money chart: from same day last month to today
                    var currentDate = DateTime.Now;
                    var startDate = currentDate.AddMonths(-1); // Same day last month
                    var endDate = currentDate; // Today

                    // Set sales period
                    SalesPeriod = $"{startDate:MMMM dd} - {endDate:MMMM dd}";

                    // Filter completed payments for the specified date range (for Daily Money chart)
                    var rangePayments = allPayments.Items
                        .Where(p => p.Status == "Success"
                                   && p.CreatedAt.Date >= startDate.Date
                                   && p.CreatedAt.Date <= endDate.Date)
                        .ToList();

                    // Group payments by date and calculate daily sales
                    var dailySales = rangePayments
                        .GroupBy(p => p.CreatedAt.Date)
                        .ToDictionary(g => g.Key, g => g.Sum(p => CalculateActualAmount(p)));

                    // Initialize data arrays
                    DailySalesData.Clear();
                    DailySalesLabels.Clear();

                    // Create data for each day in the range
                    var currentDateIter = startDate.Date;
                    while (currentDateIter <= endDate.Date)
                    {
                        // Add sales data (0 if no payments on that day)
                        decimal dailySale = dailySales.ContainsKey(currentDateIter) ? dailySales[currentDateIter] : 0;
                        DailySalesData.Add(dailySale);

                        // Add label with day and month
                        DailySalesLabels.Add($"{currentDateIter:dd/MM}");

                        currentDateIter = currentDateIter.AddDays(1);
                    }

                    // Calculate TotalSalesOneMonth from the daily sales data
                    TotalSalesOneMonth = DailySalesData.Sum();

                    _logger.LogInformation($"Total Sales (All time): {TotalSales:N0}₫");
                    _logger.LogInformation($"Total Sales (One month period): {TotalSalesOneMonth:N0}₫");
                    _logger.LogInformation($"Loaded daily sales data from {startDate:dd/MM/yyyy} to {endDate:dd/MM/yyyy}");
                }
                else
                {
                    // Fallback: create empty data for the date range
                    var currentDate = DateTime.Now;
                    var startDate = currentDate.AddMonths(-1);
                    var endDate = currentDate;

                    SalesPeriod = $"{startDate:MMMM dd} - {endDate:MMMM dd}";

                    DailySalesData.Clear();
                    DailySalesLabels.Clear();

                    var currentDateIter = startDate.Date;
                    while (currentDateIter <= endDate.Date)
                    {
                        DailySalesData.Add(0);
                        DailySalesLabels.Add($"{currentDateIter:dd/MM}");
                        currentDateIter = currentDateIter.AddDays(1);
                    }

                    TotalSales = 0;
                    TotalSalesOneMonth = 0;

                    _logger.LogWarning("Failed to load payments for daily sales, using empty data");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading daily sales data");

                // Fallback: create empty data for the date range
                var currentDate = DateTime.Now;
                var startDate = currentDate.AddMonths(-1);
                var endDate = currentDate;

                SalesPeriod = $"{startDate:MMMM dd} - {endDate:MMMM dd}";

                DailySalesData.Clear();
                DailySalesLabels.Clear();

                var currentDateIter = startDate.Date;
                while (currentDateIter <= endDate.Date)
                {
                    DailySalesData.Add(0);
                    DailySalesLabels.Add($"{currentDateIter:dd/MM}");
                    currentDateIter = currentDateIter.AddDays(1);
                }

                TotalSales = 0;
                TotalSalesOneMonth = 0;
            }
        }

        // Helper method to calculate actual amount in VND based on service type
        private decimal CalculateActualAmount(GetPaymentDto payment)
        {
            // Convert points to VND (1 point = 1000₫)
            decimal amountInVND = payment.Amount * 1000;

            // Apply percentage based on service type
            switch (payment.ServiceType?.ToLower())
            {
                case "course":
                    return amountInVND * 0.15m; // 15% for courses
                case "membership":
                    return amountInVND; // 100% for memberships
                default:
                    return amountInVND; // Default to 100% if service type is unknown
            }
        }

        private void LoadChartData()
        {
            ChartLabels.Clear();
            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;

            for (int month = 1; month <= currentMonth; month++)
            {
                ChartLabels.Add(new DateTime(currentYear, month, 1).ToString("MMMM"));
            }

            // Sample membership data - you can make this dynamic later
            MembershipsData = new List<int> { 154, 184, 175, 203, 210, 231, 240, 278, 252, 312, 320, 374 }
                .Take(currentMonth).ToList();
        }

        // API endpoint to handle AJAX requests from frontend
        public async Task<IActionResult> OnGetFilterTransactionsAsync(string? status = null, string? email = null)
        {
            try
            {
                if (!Request.Cookies.ContainsKey("AccessToken"))
                    return new JsonResult(new { success = false, message = "Unauthorized" });

                string token = Request.Cookies["AccessToken"];
                await LoadTransactions(null, null, null, false, status, token);

                var filteredTransactions = Transactions.Items.AsQueryable();

                // Filter by email if provided
                if (!string.IsNullOrWhiteSpace(email))
                {
                    filteredTransactions = filteredTransactions.Where(t =>
                        t.User != null &&
                        t.User.Email.ToLower().Contains(email.ToLower())
                    );
                }

                return new JsonResult(new
                {
                    success = true,
                    transactions = filteredTransactions.ToList(),
                    totalCount = filteredTransactions.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering transactions");
                return new JsonResult(new
                {
                    success = false,
                    message = "An error occurred while filtering transactions"
                });
            }
        }
    }
}