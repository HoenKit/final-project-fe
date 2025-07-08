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

            // ✅ 1. Nếu đã có token, chuyển hướng
            var token = Request.Cookies["AccessToken"];
            if (!string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Index");
            }

            // ✅ 2. Nếu có mã từ Google callback
            if (!string.IsNullOrEmpty(code) || !string.IsNullOrEmpty(error))
            {
                if (!string.IsNullOrEmpty(error))
                {
                    ModelState.AddModelError("", "Google login error: " + error);
                    return Page();
                }

                // Gọi backend để xử lý code và trả về token
                var client = _httpClientFactory.CreateClient();
                var callbackUrl = $"{_apiSettings.BaseUrl}/Auth/google-callback?code={code}";

                var response = await client.GetAsync(callbackUrl);
                if (!response.IsSuccessStatusCode)
                {
                    ModelState.AddModelError("", "Login failed during callback.");
                    return Page();
                }

                var newToken = await response.Content.ReadAsStringAsync();

                // ✅ Lưu token vào cookie
                Response.Cookies.Append("AccessToken", newToken, new CookieOptions
                {
                    Secure = true,
                    HttpOnly = false, // dùng JS nếu cần
                    SameSite = SameSiteMode.None,
                    Expires = DateTimeOffset.UtcNow.AddDays(3)
                });

                // ✅ Redirect sau khi lưu token

                return RedirectToPage("/Index");
            }

            // 🟡 3. Không có token và không phải callback → hiển thị form login
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
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
                                return RedirectToPage("/Admin/Dashboard/Index");
                            }

                            return RedirectToPage("/Index");
                        }
                        ModelState.AddModelError("", "Đăng nhập thất bại! Vui lòng kiểm tra thông tin.");
                    }
                }
            }

            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API đăng nhập: {ex.Message}");
                ModelState.AddModelError("", "Đã xảy ra lỗi trong quá trình đăng nhập.");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostGoogleLoginAsync()
        {
            var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync($"{_apiSettings.BaseUrl}/Auth/google-login");

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError("", "Cannot connect to Google login API.");
                return Page();
            }

            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GoogleLoginResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });


            if (result?.Url != null)
            {
                return Redirect(result.Url);
            }

            ModelState.AddModelError("", "Google login URL is invalid.");
            return Page();
        }
    }
}
