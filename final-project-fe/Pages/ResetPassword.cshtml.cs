using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text;
using final_project_fe.Utils;
using Microsoft.Extensions.Options;

namespace final_project_fe.Pages
{
    public class ResetPasswordModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ApiSettings _apiSettings;

        public ResetPasswordModel(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiSettings)
        {
            _httpClientFactory = httpClientFactory;
            _apiSettings = apiSettings.Value;
        }
        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; }
        [BindProperty]
        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "Passwords do not match.")]
        public string ConfirmPassword { get; set; }

        public string? Message { get; set; }
        public bool IsSuccess { get; set; }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Invalid information Please try again";
                return Page();
            }

            var token = Request.Query["Token"].ToString();
            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Token is missing from URL.";
                return Page();
            }

            var payload = new { newPassword = NewPassword };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var client = _httpClientFactory.CreateClient(); // dùng factory thay vì new HttpClient
            var response = await client.PostAsync($"{_apiSettings.BaseUrl}/Auth/reset-password?Token={token}", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Password reset successfully!";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                TempData["ErrorMessage"] = $"Error: {error}";
            }

            return RedirectToPage(); // redirect lại để hiển thị TempData (tránh submit form nhiều lần)
        }
    }
}
