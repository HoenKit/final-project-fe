using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;


namespace final_project_fe.Pages.Shared
{
    public class LoginModel : PageModel
    {
        private readonly ILogger<LoginModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;


        public LoginModel(ILogger<LoginModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
            _httpClientFactory = httpClientFactory;
        }

        [BindProperty]
        public LoginDto LoginData { get; set; } = new LoginDto();

        public string BaseUrl { get; set; }
        public async Task<IActionResult> OnGetAsync(string? code, string? error)
        {
            BaseUrl = _apiSettings.BaseUrl;

            //  1. If token is present, redirect
            var token = Request.Cookies["AccessToken"];
            if (!string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Index");
            }

            //  2. If there is a code from Google callback
            if (!string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    TempData["ErrorMessage"] = $"Google login error: {error}";
                    return Page();
                }

                // Call backend to process code and return token
                var client = _httpClientFactory.CreateClient();
                var callbackUrl = $"{_apiSettings.BaseUrl}/Auth/google-callback?code={code}";

                var response = await client.GetAsync(callbackUrl);
                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Login failed during callback!";
                    return Page();
                }

                var newToken = await response.Content.ReadAsStringAsync();

                //  Save tokens in cookies
                Response.Cookies.Append("AccessToken", newToken, new CookieOptions
                {
                    Secure = true,
                    HttpOnly = false, // dùng JS nếu cần
                    SameSite = SameSiteMode.None,
                    Expires = DateTimeOffset.UtcNow.AddDays(3)
                });

                //  Redirect after saving token
                TempData["SuccessMessage"] = "Login successful!";
                return RedirectToPage("/Index");
            }

            //  3. No token and no callback → show login form
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Login Error!";
                return Page();
            }


            string loginApiUrl = $"{_apiSettings.BaseUrl}/Auth/Login";

            try
            {
                var jsonContent = new StringContent(JsonSerializer.Serialize(LoginData), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _httpClient.PostAsync(loginApiUrl, jsonContent);
                string responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation($"Login API Response: {responseContent}");

                if (response.IsSuccessStatusCode)
                {

                    if (responseContent.Trim().StartsWith("{"))
                    {
                        var loginResponse = JsonSerializer.Deserialize<LoginResponseDto>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                        {
                            Response.Cookies.Append("AccessToken", loginResponse.Token, new CookieOptions
                            {
                                HttpOnly = false,
                                Secure = true,
                                SameSite = SameSiteMode.None,
                                Expires = DateTime.UtcNow.AddDays(7)
                            });

                            string? role = JwtHelper.GetRoleFromToken(loginResponse.Token);
                            string? userId = JwtHelper.GetUserIdFromToken(loginResponse.Token);

                            _logger.LogInformation($"User Role: {role}");
                            _logger.LogInformation($"User Id: {userId}");


                            if (role == "Admin")
                            {
                                TempData["SuccessMessage"] = "Admin login successful!";
                                return RedirectToPage("/Admin/Dashboard/Index");
                            }
                            TempData["SuccessMessage"] = "Login successful!";
                            return RedirectToPage("/Index");
                        }
                        TempData["ErrorMessage"] = "Login failed! Please check your information.";
                    }
                }
            }

            catch (Exception ex)
            {
                _logger.LogError($"Error calling login API: {ex.Message}");
                TempData["ErrorMessage"] = "An error occurred during login.";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostGoogleLoginAsync()
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{_apiSettings.BaseUrl}/Auth/google-login");

            if (!response.IsSuccessStatusCode)
            {
                TempData["ErrorMessage"] = "Cannot connect to Google login API.";
                return Page();
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GoogleLoginResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });


            if (result?.Url != null)
            {
                return Redirect(result.Url);
            }

            TempData["ErrorMessage"] = "Google login URL is invalid.";
            return Page();
        }
    }
}
