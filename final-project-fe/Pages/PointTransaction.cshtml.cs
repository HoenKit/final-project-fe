using final_project_fe.Dtos.Users;
using final_project_fe.Pages.Mentor.MentorPage;
using final_project_fe.Services;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace final_project_fe.Pages
{
    public class PointTransactionModel : PageModel
    {
        private readonly ILogger<PointTransactionModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private readonly PayOSService _payOSService;
        public PointTransactionModel(ILogger<PointTransactionModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient, PayOSService payOSService)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
            _payOSService = payOSService;
        }
        public User UserInfo { get; set; } = new User();
        public string BaseUrl { get; set; }
        public string CurrentUserId { get; set; }
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";
        [BindProperty]
        [Required(ErrorMessage = "Amount is required")]
        [Range(10000, 50000000, ErrorMessage = "Amount must be between 10,000 and 50,000,000")]
        public int Amount { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Points is required")]
        public int Points { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Payment method is required")]
        public string PaymentMethod { get; set; } = "payos";

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
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }

                // Get User by ID
                var userResponse = await _httpClient.GetAsync($"{BaseUrl}/User/GetUserById/{CurrentUserId}");
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

                if (UserInfo == null)
                {
                    ModelState.AddModelError("", "User does not exist.");
                    return Page();
                }

                if (UserInfo.UserMetaData.Avatar != null)
                {
                    UserInfo.UserMetaData.Avatar = ImageUrlHelper.AppendSasTokenIfNeeded(UserInfo.UserMetaData.Avatar, SasToken);
                }
                return Page();
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while processing your request.");
                return Page();
            }
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }
            BaseUrl = _apiSettings.BaseUrl;
            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }

                // Get User by ID
                var userResponse = await _httpClient.GetAsync($"{BaseUrl}/User/{CurrentUserId}");
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

                if (UserInfo == null)
                {
                    ModelState.AddModelError("", "User does not exist.");
                    return Page();
                }

                if (UserInfo.UserMetaData.Avatar != null)
                {
                    UserInfo.UserMetaData.Avatar = ImageUrlHelper.AppendSasTokenIfNeeded(UserInfo.UserMetaData.Avatar, SasToken);
                }
                // Validate points calculation
                var expectedPoints = Amount / 1000;
                if (Points != expectedPoints)
                {
                    ModelState.AddModelError("", "Invalid points calculation");
                    return Page();
                }

                // Create PayOS payment request
                var paymentResult = await _payOSService.CreatePayOSPayment(Amount, Points, UserInfo);

                if (paymentResult.Success)
                {
                    // Redirect to PayOS payment page
                    return Redirect(paymentResult.PaymentUrl);
                }
                else
                {
                    ModelState.AddModelError("", "Payment creation failed. Please try again.");
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "An error occurred while processing your request.");
                return Page();
            }
        }
    }
}
