using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Module;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace final_project_fe.Pages
{
    public class UserProfileModel : PageModel
    {
        private readonly ILogger<UserProfileModel> _logger;
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;
        private readonly SignalrSetting _signalrSetting;
        
        public UserProfileModel(ILogger<UserProfileModel> logger, HttpClient httpClient, IOptions<ApiSettings> apiSettings, SignalrSetting signalrSetting)
        {
            _logger = logger;
            _httpClient = httpClient;
            _apiSettings = apiSettings.Value;
            _signalrSetting = signalrSetting;
        }
        public string BaseUrl { get; set; }
        [BindProperty]
        public User Profile { get; set; } = new User();
        public string CurrentUserId { get; set; }
        public async Task<IActionResult> OnGetAsync()
        {
            BaseUrl = _apiSettings.BaseUrl;
            if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }
                // Gọi API để lấy thông tin user hiện tại
                var response = await _httpClient.GetAsync($"{BaseUrl}/User/GetUserById/{CurrentUserId}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Unable to get user information. Status: {StatusCode}", response.StatusCode);
                    TempData["ErrorMessage"] = $"Error calling API: {response.StatusCode}";
                    return Page();
                }
                var jsonContent = await response.Content.ReadAsStringAsync();
                var userInfo = JsonSerializer.Deserialize<User>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (userInfo == null)
                {
                    ModelState.AddModelError("", "Can not find User Profile.");
                    TempData["ErrorMessage"] = "Can not find Profile!";
                    return Page();
                }
                Profile = userInfo;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Error connecting to API server.");
                TempData["ErrorMessage"] = "Error connecting to server API.";
                return Page();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error processing JSON data from API.");
                TempData["ErrorMessage"] = "Error processing data from server.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unknown error in OnGetAsync.");
                TempData["ErrorMessage"] = "An unexpected error occurred.";
                return Page();
            }

            return Page();
        }

        // Handler Update Profile
        public async Task<IActionResult> OnPostAsync()
        {
            if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            BaseUrl = _apiSettings.BaseUrl;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }
                if (string.IsNullOrEmpty(CurrentUserId))
                {
                    TempData["ErrorMessage"] = "Unable to identify current user";
                    return Page();
                }

                // Kiểm tra model validation
                //if (!ModelState.IsValid)
                //{
                //    TempData["ErrorMessage"] = "Please correct the errors and try again";
                //    return Page();
                //}

                /*var form = new MultipartFormDataContent();
                form.Add(new StringContent(Profile.Email ?? ""), "Email");
                form.Add(new StringContent(Profile.Phone ?? ""), "Phone");
                form.Add(new StringContent(Profile.UserMetaData.FirstName ?? ""), "FirstName");
                form.Add(new StringContent(Profile.UserMetaData.LastName ?? ""), "LastName");
                form.Add(new StringContent(Profile.UserMetaData.Address ?? ""), "Address");
                form.Add(new StringContent(Profile.UserMetaData.Nationality ?? ""), "Nationality");
                form.Add(new StringContent(Profile.UserMetaData.Gender ?? ""), "Gender");
                form.Add(new StringContent(Profile.UserMetaData.FavouriteSubject ?? ""), "FavouriteSubject");
                form.Add(new StringContent(Profile.UserMetaData.Level ?? ""), "Level");
                form.Add(new StringContent(Profile.UserMetaData.Goals ?? ""), "Goals");
                form.Add(new StringContent(Profile.UserMetaData.Birthday.Value.ToString("MM-dd-yyyy")), "Birthday");*/

                var jsonContent = new StringContent(
                   JsonSerializer.Serialize(Profile.UserMetaData),
                   Encoding.UTF8,
                   "application/json"
               );

                var response = await _httpClient.PutAsync($"{BaseUrl}/User/Update/{CurrentUserId}", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Kiểm tra response content có rỗng không
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                        var updateProfile = JsonSerializer.Deserialize<User>(responseContent, options);
                        if (updateProfile != null)
                        {
                            Profile = updateProfile;
                        }
                    }

                    TempData["SuccessMessage"] = "Profile updated successfully!";
                    return RedirectToPage(); 
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Update profile failed! Status: " + response.StatusCode);
                    TempData["ErrorMessage"] = "Failed to update profile: " + errorContent;
                    return Page();
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Network error while updating profile");
                TempData["ErrorMessage"] = "Network error occurred. Please check your connection and try again.";
                return Page();
            }
            catch (TaskCanceledException tcEx)
            {
                _logger.LogError(tcEx, "Request timeout while updating profile");
                TempData["ErrorMessage"] = "Request timeout. Please try again.";
                return Page();
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing error while updating profile");
                TempData["ErrorMessage"] = "Invalid response from server. Please try again.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating profile");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again later.";
                return Page();
            }
        }
    }
}
