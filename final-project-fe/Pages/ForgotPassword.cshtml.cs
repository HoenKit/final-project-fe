using final_project_fe.Utils;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

namespace final_project_fe.Pages
{
    public class ForgotPasswordModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApiSettings _apiSettings;

        public ForgotPasswordModel(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiSettings)
        {
            _httpClientFactory = httpClientFactory;
            _apiSettings = apiSettings.Value;
        }
        [BindProperty]
        [Required, EmailAddress]
        public string Email { get; set; }
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }
        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Email))
            {
                Message = "Please enter your email.";
                IsSuccess = false;
                return Page();
            }
            var client = _httpClientFactory.CreateClient();
            try
            {
                var request = new ForgotPasswordRequest { Email = Email };
                var url = $"{_apiSettings.BaseUrl}/Auth/forgot-password";

                var response = await client.PostAsJsonAsync(url, request);

                if (response.IsSuccessStatusCode)
                {
                    Message = "A reset link has been sent to your email.";
                    IsSuccess = true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Message = $"Error: {error}";
                    IsSuccess = false;
                }
            }
            catch (HttpRequestException ex)
            {
                Message = $"Request failed: {ex.Message}";
                IsSuccess = false;
            }

            return Page();
        }
    }
}

