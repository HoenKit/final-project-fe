﻿using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text;

namespace final_project_fe.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly ILogger<RegisterModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public RegisterModel(ILogger<RegisterModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }
        [BindProperty]
        public RegisterDto RegisterData { get; set; } = new RegisterDto
        {
            UserMetadataDto = new UserMetadataDto()
        };
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

            if (RegisterData.Password != RegisterData.ConfirmPassword)
            {
                ModelState.AddModelError("RegisterData.ConfirmPassword", "Mật khẩu xác nhận không khớp.");
                return Page();
            }

            string registerApiUrl = $"{_apiSettings.BaseUrl}/Auth/Register";

            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };

                var jsonContent = new StringContent(JsonSerializer.Serialize(RegisterData, options), Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.PostAsync(registerApiUrl, jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage("/Login");
                }
                else
                {
                    _logger.LogError($"Lỗi API Đăng Ký: {response.StatusCode}");
                    ModelState.AddModelError(string.Empty, "Đăng ký thất bại! Vui lòng kiểm tra lại thông tin.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi trong quá trình đăng ký.");
            }

            return Page();
        }
    }
}

