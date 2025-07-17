using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Withdraw;
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
using final_project_fe.Dtos.Users;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class RequestWithdrawalModel : PageModel
    {
        private readonly ILogger<RequestWithdrawalModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public RequestWithdrawalModel(ILogger<RequestWithdrawalModel> logger,
                                    IOptions<ApiSettings> apiSettings,
                                    HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public string CurrentUserId { get; set; }
        public string CurrentMentorId { get; set; }
        public PageResult<WithdrawDto> Withdraw { get; set; }
        public List<string> UserRoles { get; private set; } = new List<string>();
        public GetMentorDto Mentor { get; set; }
        public User User { get; set; }

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

            await LoadDashboardData(token, CurrentUserId);
            return Page();
        }

        private async Task LoadDashboardData(string token, string CurrentUserId)
        {
            await LoadMentors(token, CurrentUserId);
            await LoadUsers(token, CurrentUserId);
            await LoadWithdraws(token, CurrentMentorId);
        }

        private async Task LoadWithdraws(string token, string mentorId)
        {
            try
            {
                var withdrawUrl = new UriBuilder($"{_apiSettings.BaseUrl}/Withdraw");
                var withdrawQuery = HttpUtility.ParseQueryString(string.Empty);

                withdrawQuery["page"] = "1";
                withdrawQuery["pageSize"] = "1000";
                withdrawQuery["sortOption"] = "desc_date";
                withdrawQuery["mentorId"] = mentorId;

                withdrawUrl.Query = withdrawQuery.ToString();

                var withdrawRequest = new HttpRequestMessage(HttpMethod.Get, withdrawUrl.ToString());
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

        private async Task LoadMentors(string token, string userId)
        {
            try
            {
                var mentorUrl = new UriBuilder($"{_apiSettings.BaseUrl}/Mentor/get-by-user/{userId}");
                var mentorRequest = new HttpRequestMessage(HttpMethod.Get, mentorUrl.ToString());
                mentorRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var mentorResponse = await _httpClient.SendAsync(mentorRequest);
                if (mentorResponse.IsSuccessStatusCode)
                {
                    var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                    Mentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new GetMentorDto();
                }
                else
                {
                    Mentor = new GetMentorDto();
                }

                CurrentMentorId = Mentor.MentorId.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mentor");
                Mentor = new GetMentorDto();
            }
        }

        private async Task LoadUsers(string token, string userId)
        {
            try
            {
                var userUrl = new UriBuilder($"{_apiSettings.BaseUrl}/User/GetUserById/{userId}");
                var userRequest = new HttpRequestMessage(HttpMethod.Get, userUrl.ToString());
                userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var userResponse = await _httpClient.SendAsync(userRequest);
                if (userResponse.IsSuccessStatusCode)
                {
                    var userJson = await userResponse.Content.ReadAsStringAsync();
                    User = JsonSerializer.Deserialize<User>(userJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new User();
                }
                else
                {
                    User = new User();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user");
                User = new User();
            }
        }

        public async Task<IActionResult> OnPostAsync([FromForm] string CurrentMentorId, [FromForm] decimal points)
        {
            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                {
                    TempData["ErrorMessage"] = "Please login to continue.";
                    return RedirectToPage("/Login");
                }

                // Calculate amount based on points (1 point = 1000 VND)
                decimal amount = points * 1000;

                var withdraw = new CreateWithdraw
                {
                    MentorId = int.Parse(CurrentMentorId),
                    Points = points,
                    Amount = amount
                };

                var content = new StringContent(JsonSerializer.Serialize(withdraw),
                    System.Text.Encoding.UTF8, "application/json");

                string apiUrl = $"{_apiSettings.BaseUrl}/Withdraw";
                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Withdrawal request submitted successfully!";
                }
                else
                {
                    TempData["ErrorMessage"] = "Failed to submit withdrawal request. Please try again.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error submitting withdrawal: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred while processing your request.";
            }

            return RedirectToPage("./RequestWithdrawal");
        }

        public async Task<IActionResult> OnGetCheckBankInfo()
        {
            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                {
                    return new JsonResult(new { hasBankInfo = false });
                }

                // Get mentor info
                var mentorUrl = new UriBuilder($"{_apiSettings.BaseUrl}/Mentor/get-by-user/{CurrentUserId}");
                var mentorRequest = new HttpRequestMessage(HttpMethod.Get, mentorUrl.ToString());
                mentorRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var mentorResponse = await _httpClient.SendAsync(mentorRequest);
                if (mentorResponse.IsSuccessStatusCode)
                {
                    var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                    var mentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    bool hasBankInfo = !string.IsNullOrEmpty(mentor?.AccountNumber) &&
                                     !string.IsNullOrEmpty(mentor?.AccountName) &&
                                     !string.IsNullOrEmpty(mentor?.AccountBank);

                    return new JsonResult(new { hasBankInfo });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking bank info");
            }

            return new JsonResult(new { hasBankInfo = false });
        }
    }
}