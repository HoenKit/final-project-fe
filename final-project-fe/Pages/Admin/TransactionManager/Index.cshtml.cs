using final_project_fe.Dtos.Transaction;
using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;

namespace final_project_fe.Pages.Admin.TransactionManager
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

        public string CurrentUserId { get; set; }
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

            await LoadDashboardData(token, status);

            return Page();
        }

        private async Task LoadDashboardData(string token, string? status)
        {
            // Load transactions for current month only (for display)
            await LoadTransactions(null, null, null, false, status, token);

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

                if (userId.HasValue)
                    transactionQuery["userId"] = userId.Value.ToString();

                if (filterByUser && !string.IsNullOrWhiteSpace(CurrentUserId))
                    transactionQuery["userId"] = CurrentUserId;

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
                    Transactions = allTransactions;
                }
                else
                {
                    Transactions = new PageResult<GetTransactionDto>(new List<GetTransactionDto>(), 0, 1, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all transactions");
                Transactions = new PageResult<GetTransactionDto>(new List<GetTransactionDto>(), 0, 1, 0);
            }
        }

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
