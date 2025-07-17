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
using final_project_fe.Dtos.Withdraw;
using final_project_fe.Dtos.Mentors;

namespace final_project_fe.Pages.Admin.TransactionManager
{
    public class WithdrawalRequestModel : PageModel
    {
        private readonly ILogger<WithdrawalRequestModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public WithdrawalRequestModel(ILogger<WithdrawalRequestModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public string CurrentMentorId { get; set; }
        public PageResult<WithdrawDto> Withdraw { get; set; }
        public PageResult<GetMentorDto> Mentor { get; set; }


        public async Task<IActionResult> OnGetAsync()
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            if (!string.IsNullOrEmpty(token))
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                CurrentMentorId = jsonToken?.Claims
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

            await LoadDashboardData(token);

            return Page();
        }

        private async Task LoadDashboardData(string token)
        {
            await LoadWithdraws(token);
            await LoadMemtors(token);
        }

        private async Task LoadWithdraws(string token)
        {
            try
            {
                var withdrawUrl = new UriBuilder($"{_apiSettings.BaseUrl}/Withdraw");
                var withdrawQuery = HttpUtility.ParseQueryString(string.Empty);

                withdrawQuery["page"] = "1";
                withdrawQuery["pageSize"] = "1000";
                withdrawQuery["sortOption"] = "desc_date";
                withdrawQuery["status"] = "Pending";

                withdrawUrl.Query = withdrawQuery.ToString();

                var withdrawRequest = new HttpRequestMessage(HttpMethod.Get, withdrawUrl.ToString());
                withdrawRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var withdrawResponse = await _httpClient.SendAsync(withdrawRequest);
                if (withdrawResponse.IsSuccessStatusCode)
                {
                    var withdrawJson = await withdrawResponse.Content.ReadAsStringAsync();
                    var allWithdraws = JsonSerializer.Deserialize<PageResult<WithdrawDto>>(withdrawJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<WithdrawDto>(new List<WithdrawDto>(), 0, 1, 1000);
                    Withdraw = allWithdraws;
                }
                else
                {
                    Withdraw = new PageResult<WithdrawDto>(new List<WithdrawDto>(), 0, 1, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all withdraws");
                Withdraw = new PageResult<WithdrawDto>(new List<WithdrawDto>(), 0, 1, 0);
            }
        }

        private async Task LoadMemtors(string token)
        {
            try
            {
                var withdrawUrl = new UriBuilder($"{_apiSettings.BaseUrl}/Mentor");
                var withdrawQuery = HttpUtility.ParseQueryString(string.Empty);

                withdrawUrl.Query = withdrawQuery.ToString();

                var withdrawRequest = new HttpRequestMessage(HttpMethod.Get, withdrawUrl.ToString());
                withdrawRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var withdrawResponse = await _httpClient.SendAsync(withdrawRequest);
                if (withdrawResponse.IsSuccessStatusCode)
                {
                    var withdrawJson = await withdrawResponse.Content.ReadAsStringAsync();
                    var allWithdraws = JsonSerializer.Deserialize<PageResult<GetMentorDto>>(withdrawJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<GetMentorDto>(new List<GetMentorDto>(), 0, 1, 1000);
                    Mentor = allWithdraws;
                }
                else
                {
                    Mentor = new PageResult<GetMentorDto>(new List<GetMentorDto>(), 0, 1, 0);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading all withdraws");
                Withdraw = new PageResult<WithdrawDto>(new List<WithdrawDto>(), 0, 1, 0);
            }
        }
    }
}
