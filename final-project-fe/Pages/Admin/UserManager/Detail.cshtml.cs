using final_project_fe.Dtos.UserManager;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace final_project_fe.Pages.Admin.UserManager
{
    public class DetailModel : PageModel
    {
        private readonly ILogger<DetailModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public DetailModel(ILogger<DetailModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public UserManagerDto UserDetail { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(Guid userId)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
            {
                return RedirectToPage("/Login");
            }

            string token = Request.Cookies["AccessToken"];

            string? role = JwtHelper.GetRoleFromToken(token);
            if (role != "Admin")
            {
                return RedirectToPage("/Index");
            }

            if (userId == Guid.Empty)
            {
                return BadRequest("Invalid user ID.");
            }

            string apiUrl = $"{_apiSettings.BaseUrl}/UserManager/{userId}";

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    UserDetail = JsonSerializer.Deserialize<UserManagerDto>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new UserManagerDto();

                    if (UserDetail.UserProfile == null)
                    {
                        UserDetail.UserProfile = new UserProfileDto
                        {
                            FirstName = "N/A",
                            LastName = "N/A",
                            Gender = "Unknown",
                            Address = "Not Available"
                        };
                    }
                }
                else
                {
                    _logger.LogError($"Failed to fetch user details: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calling API: {ex.Message}");
            }

            return Page();
        }
    }
}
