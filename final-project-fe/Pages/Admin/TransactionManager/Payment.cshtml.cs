using final_project_fe.Dtos.Transaction;
using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Web;
using final_project_fe.Dtos.Payment;

namespace final_project_fe.Pages.Admin.TransactionManager
{
    public class PaymentModel : PageModel
    {
        private readonly ILogger<PaymentModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public PaymentModel(ILogger<PaymentModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public string CurrentUserId { get; set; }
        public PageResult<GetPaymentDto> Payments { get; set; }

        public async Task<IActionResult> OnGetAsync(string? serviceType = null, string? email = null)
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

            await LoadDashboardData(token, serviceType);

            return Page();
        }

        private async Task LoadDashboardData(string token, string? serviceType)
        {
            // Load transactions for current month only (for display)
            await LoadPayments(null, null, null, false, serviceType, token);

        }

        private async Task LoadPayments(int? currentPage, Guid? userId, string? sortOption, bool filterByUser, string? serviceType, string token)
        {
            try
            {
                var paymentUrl = new UriBuilder($"{_apiSettings.BaseUrl}/Payment");
                var paymentQuery = HttpUtility.ParseQueryString(string.Empty);

                paymentQuery["page"] = "1";
                paymentQuery["pageSize"] = "1000";
                paymentQuery["sortOption"] = "desc_date";

                if (userId.HasValue)
                    paymentQuery["userId"] = userId.Value.ToString();

                if (filterByUser && !string.IsNullOrWhiteSpace(CurrentUserId))
                    paymentQuery["userId"] = CurrentUserId;

                if (!string.IsNullOrWhiteSpace(serviceType) && serviceType != "all")
                {
                    var serviceTypeList = new List<string>();
                    if (serviceType.Contains("Course"))
                        serviceTypeList.Add("Course");
                    if (serviceType.Contains("Membership"))
                        serviceTypeList.Add("Membership");

                    foreach (var s in serviceTypeList)
                    {
                        paymentQuery.Add("serviceTypees", s);
                    }
                }

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
                    }) ?? new PageResult<GetPaymentDto>(new List<GetPaymentDto>(), 0, 1, 1000);
                    Payments = allPayments;
                }
                else
                {
                    Payments = new PageResult<GetPaymentDto>(new List<GetPaymentDto>(), 0, 1, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all payments");
                Payments = new PageResult<GetPaymentDto>(new List<GetPaymentDto>(), 0, 1, 0);
            }
        }

        public async Task<IActionResult> OnGetFilterPaymentsAsync(string? serviceType = null, string? email = null)
        {
            try
            {
                if (!Request.Cookies.ContainsKey("AccessToken"))
                    return new JsonResult(new { success = false, message = "Unauthorized" });

                string token = Request.Cookies["AccessToken"];
                await LoadPayments(null, null, null, false, serviceType, token);

                var filteredPayments = Payments.Items.AsQueryable();

                // Filter by service type if provided
                if (!string.IsNullOrWhiteSpace(serviceType) && serviceType != "all")
                {
                    filteredPayments = filteredPayments.Where(t =>
                        t.ServiceType == serviceType
                    );
                }

                // Filter by email if provided
                if (!string.IsNullOrWhiteSpace(email))
                {
                    filteredPayments = filteredPayments.Where(t =>
                        t.Email.ToLower().Contains(email.ToLower())
                    );
                }

                return new JsonResult(new
                {
                    success = true,
                    payments = filteredPayments.ToList(),
                    totalCount = filteredPayments.Count()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error filtering payments");
                return new JsonResult(new
                {
                    success = false,
                    message = "An error occurred while filtering payments"
                });
            }
        }
    }
}
