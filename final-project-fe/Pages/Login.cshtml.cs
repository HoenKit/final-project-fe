using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.Json;
using final_project_fe.Dtos.Users;
using System.Security.Claims;


namespace final_project_fe.Pages.Shared
{
    public class LoginModel : PageModel
    {
        private readonly ILogger<LoginModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public LoginModel(ILogger<LoginModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        [BindProperty]
        public LoginDto LoginData { get; set; } = new LoginDto();

        public async Task OnGetAsync()
        {
            if (Request.Cookies.ContainsKey("AccessToken"))
            {
                Response.Redirect("/Index");
            }
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
                                SameSite = SameSiteMode.Strict,
                                Expires = DateTime.UtcNow.AddDays(7)
                            });

                            string? role = JwtHelper.GetRoleFromToken(loginResponse.Token);
                            _logger.LogInformation($"User Role: {role}");

                            if (role == "Admin")
                            {
                                return RedirectToPage("/Admin/Dashboard/Index");
                            }

                            return RedirectToPage("/Index");
                        }
                    }
                    ModelState.AddModelError("", "Đăng nhập thất bại! Vui lòng kiểm tra thông tin.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API đăng nhập: {ex.Message}");
                ModelState.AddModelError("", "Đã xảy ra lỗi trong quá trình đăng nhập.");
            }

            return Page();
        }
    }
}
