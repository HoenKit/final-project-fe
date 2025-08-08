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

            // 1. Nếu đã có token thì redirect luôn
            var token = Request.Cookies["AccessToken"];
            if (!string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Index");
            }

            // 2. Nếu có lỗi từ Google callback
            if (!string.IsNullOrEmpty(error))
            {
                TempData["ErrorMessage"] = $"Google login error: {error}";
                return Page();
            }

            // 3. Nếu có code từ Google
            if (!string.IsNullOrEmpty(code))
            {
                var client = _httpClientFactory.CreateClient();
                var callbackUrl = $"{_apiSettings.BaseUrl}/Auth/google-callback?code={code}";

                var response = await client.GetAsync(callbackUrl);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Xử lý lỗi từ API (nếu có message)
                    if (responseContent.Trim().StartsWith("{"))
                    {
                        var errorObj = JsonSerializer.Deserialize<Dictionary<string, string>>(responseContent);
                        if (errorObj != null && errorObj.TryGetValue("message", out var errorMsg))
                        {
                            TempData["ErrorMessage"] = $"Login failed: {errorMsg}";
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Login failed during callback!";
                        }
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Login failed during callback!";
                    }

                    return Page();
                }

                // Nếu thành công, kiểm tra phản hồi là JSON có token
                var tokenObj = JsonSerializer.Deserialize<Dictionary<string, string>>(responseContent);
                if (tokenObj != null && tokenObj.TryGetValue("token", out var newToken))
                {
                    Response.Cookies.Append("AccessToken", newToken, new CookieOptions
                    {
                        Secure = true,
                        HttpOnly = false,
                        SameSite = SameSiteMode.None,
                        Expires = DateTimeOffset.UtcNow.AddDays(3)
                    });

                    TempData["SuccessMessage"] = "Login successful!";
                    return RedirectToPage("/Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Login failed! Please check your information.";
                    return Page();
                }
            }

            // 4. Không có token, không có code → hiển thị login form
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
                else
                {
                    // Nếu API trả về lỗi và responseContent là JSON chứa "message"
                    if (responseContent.Trim().StartsWith("{"))
                    {
                        var errorObj = JsonSerializer.Deserialize<Dictionary<string, string>>(responseContent);
                        if (errorObj != null && errorObj.TryGetValue("message", out var errorMessage))
                        {
                            TempData["ErrorMessage"] = errorMessage;
                        }
                        else
                        {
                            TempData["ErrorMessage"] = "Login failed!";
                        }
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Login failed!";
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
            try
            {
                var client = _httpClientFactory.CreateClient();
                var response = await client.GetAsync($"{_apiSettings.BaseUrl}/Auth/google-login");

                var json = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    // Deserialize để lấy message lỗi (nếu có)
                    var errorDoc = JsonSerializer.Deserialize<JsonElement>(json);
                    if (errorDoc.TryGetProperty("message", out var errorMessageProp))
                    {
                        TempData["ErrorMessage"] = $"Google login failed: {errorMessageProp.GetString()}";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Google login failed due to unknown error.";
                    }
                    return Page();
                }

                // Nếu thành công thì xử lý như bình thường
                var jsonDoc = JsonSerializer.Deserialize<JsonElement>(json);

                // Kiểm tra nếu có message nhưng không có url => lỗi
                if (jsonDoc.TryGetProperty("message", out var messageProp) &&
                    !jsonDoc.TryGetProperty("url", out _))
                {
                    var errorMessage = messageProp.GetString() ?? "Unknown error during Google login.";
                    TempData["ErrorMessage"] = $"Google login failed: {errorMessage}";
                    return Page();
                }

                // Nếu có URL thì redirect
                var result = JsonSerializer.Deserialize<GoogleLoginResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (!string.IsNullOrEmpty(result?.Url))
                {
                    return Redirect(result.Url);
                }

                TempData["ErrorMessage"] = "Google login URL is invalid.";
                return Page();
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"An unexpected error occurred: {ex.Message}";
                return Page();
            }
        }
    }
}
