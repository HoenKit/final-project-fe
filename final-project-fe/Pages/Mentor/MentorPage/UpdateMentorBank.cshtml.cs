using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Text;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Withdraw;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class UpdateMentorBankModel : PageModel
    {
        private readonly ILogger<UpdateMentorBankModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public UpdateMentorBankModel(ILogger<UpdateMentorBankModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        [BindProperty]
        public UpdateMentorBankDto Mentor { get; set; } = new();
        public string CurrentUserId { get; private set; } = string.Empty;
        public List<string> UserRoles { get; private set; } = new List<string>();

        public async Task<IActionResult> OnPostAsync(int mentorId, string accountNumber, string accountName, string accountBank)
        {
            // Check authentication
            if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Please login to access this page.";
                return RedirectToPage("/Login");
            }

            // Parse JWT token
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

            // Check if user is mentor
            if (UserRoles == null || !UserRoles.Contains("Mentor"))
            {
                TempData["ErrorMessage"] = "Access denied. You must be a mentor to view this page.";
                return RedirectToPage("/Index");
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                Mentor = new UpdateMentorBankDto
                {
                    MentorId = mentorId,
                    AccountNumber = accountNumber,
                    AccountName = accountName,
                    AccountBank = accountBank
                };

                var json = JsonSerializer.Serialize(Mentor, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var content = new StringContent(json, Encoding.UTF8, "application/json");
                string apiUrl = $"{_apiSettings.BaseUrl}/Mentor";
                var response = await _httpClient.PutAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = $"Request sent successfully";
                    return RedirectToPage("./RequestWithdrawal");
                }
                else
                {
                    TempData["ErrorMessage"] = $"Request sent failed";
                    return RedirectToPage("./RequestWithdrawal");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API UpdateMetadata: {ex.Message}");
            }

            return Page();
        }
    }
}
