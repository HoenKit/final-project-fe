using final_project_fe.Dtos.Payment;
using final_project_fe.Dtos.Users;
using final_project_fe.Services;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Numerics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace final_project_fe.Pages
{
    public class UpdatePremiumModel : PageModel
    {
        private readonly ILogger<UpdatePremiumModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        public UpdatePremiumModel(ILogger<UpdatePremiumModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }
        public User UserInfo { get; set; } = new User();
        public string BaseUrl { get; set; }
        public string CurrentUserId { get; set; }
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";
        public List<PremiumPackage> PremiumPackages { get; set; } = new List<PremiumPackage>();
        public async Task<IActionResult> OnGetAsync()
        {
            BaseUrl = _apiSettings.BaseUrl;

            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }

                if (string.IsNullOrEmpty(CurrentUserId))
                    return RedirectToPage("/Login");

                // 🔹 Get User by ID with token
                using var userRequest = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/User/GetUserById/{CurrentUserId}");
                userRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var userResponse = await _httpClient.SendAsync(userRequest);

                if (!userResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Can not get User.");
                    return Page();
                }

                var userJson = await userResponse.Content.ReadAsStringAsync();
                UserInfo = JsonSerializer.Deserialize<User>(userJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (UserInfo?.UserMetaData?.Avatar != null)
                {
                    UserInfo.UserMetaData.Avatar = ImageUrlHelper.AppendSasTokenIfNeeded(UserInfo.UserMetaData.Avatar, SasToken);
                }

                // 🔹 Get Premium Packages with token
                using var packageRequest = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/Payment/Premium-package");
                packageRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var packageResponse = await _httpClient.SendAsync(packageRequest);

                if (packageResponse.IsSuccessStatusCode)
                {
                    var packageJson = await packageResponse.Content.ReadAsStringAsync();
                    PremiumPackages = JsonSerializer.Deserialize<List<PremiumPackage>>(packageJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<PremiumPackage>();
                }
                else
                {
                    _logger.LogWarning("Can not get Premium Packages.");
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in OnGetAsync");
                ModelState.AddModelError("", "An error occurred while processing your request.");
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync(int PlanId)
        {
            if (PlanId <= 0)
            {
                ModelState.AddModelError("", "Please select a Premium plan.");
                return Page();
            }

            BaseUrl = _apiSettings.BaseUrl;

            if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
            var userId = jwtToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userId))
                return RedirectToPage("/Login");

            var payload = new { UserId = userId, planId = PlanId };
            var json = JsonSerializer.Serialize(payload);

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/Payment/buy-premium")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Membership purchased successfully";
                return RedirectToPage("/UserPage");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"Unauthorized or failed request: {responseBody}");
                return Page();
            }
        }
    }
}

