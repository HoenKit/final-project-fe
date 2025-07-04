using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using final_project_fe.Dtos;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Payment;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace final_project_fe.Pages
{
    public class CourseRecommendModel : PageModel
    {
        private readonly ILogger<CourseRecommendModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public CourseRecommendModel(ILogger<CourseRecommendModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public User UserInfo { get; set; }
        public List<CourseRecommendDto> RecommendedCourses { get; set; } = new();
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";
        public bool HasCompleteProfile { get; set; }
        public string CurrentUserId { get; private set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // 1. Check authentication and get userId from token
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                {
                    TempData["ErrorMessage"] = "Please login to access this page.";
                    return RedirectToPage("/Login");
                }

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                CurrentUserId = jsonToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

                if (string.IsNullOrEmpty(CurrentUserId))
                {
                    TempData["ErrorMessage"] = "Invalid user information.";
                    return RedirectToPage("/Login");
                }

                var userResponse = await _httpClient.GetAsync($"{_apiSettings.BaseUrl}/User/GetUserById/{CurrentUserId}");
                if (!userResponse.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Failed to load user info. Status: {StatusCode}", userResponse.StatusCode);
                    TempData["ErrorMessage"] = "Failed to load user information.";
                    return Page();
                }

                var userJson = await userResponse.Content.ReadAsStringAsync();
                UserInfo = JsonSerializer.Deserialize<User>(userJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new User();

                HasCompleteProfile = UserInfo?.UserMetaData != null &&
                                    !string.IsNullOrWhiteSpace(UserInfo.UserMetaData.Level) &&
                                    !string.IsNullOrWhiteSpace(UserInfo.UserMetaData.Goals) &&
                                    !string.IsNullOrWhiteSpace(UserInfo.UserMetaData.FavouriteSubject);

                if (!HasCompleteProfile)
                {
                    _logger.LogInformation("User {UserId} doesn't have complete profile", CurrentUserId);
                    TempData["WarningMessage"] = "Please complete your profile to get recommendations.";
                    return Page();
                }

                var response = await _httpClient.GetAsync($"{_apiSettings.BaseUrl}/Course/recommend-course?userId={CurrentUserId}");

                if (!response.IsSuccessStatusCode)
                {
                    TempData["WarningMessage"] = "No recommendations available.";
                    return Page();
                }

                var json = await response.Content.ReadAsStringAsync();
                RecommendedCourses = JsonSerializer.Deserialize<List<CourseRecommendDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new();

                foreach (var course in RecommendedCourses)
                {
                    course.Score = Math.Round((course.Score ?? 0) * 100, 0, MidpointRounding.AwayFromZero);

                    if (!string.IsNullOrWhiteSpace(course.CoursesImage))
                    {
                        course.CoursesImage = ImageUrlHelper.AppendSasTokenIfNeeded(course.CoursesImage, SasToken);
                    }
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading course recommendations");
                TempData["ErrorMessage"] = "An error occurred while loading recommendations.";
                return RedirectToPage("/Error");
            }
        }

        public async Task<IActionResult> OnPostAsync(int? courseId)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                string token = Request.Cookies["AccessToken"];
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                var userId = jsonToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    ModelState.AddModelError("", "Please login before purchasing.");
                    return RedirectToPage("/Login");
                }

                var request = new BuyCourseRequest
                {
                    UserId = userId,
                    CourseId = courseId ?? 0,
                };

                var api = $"{_apiSettings.BaseUrl}/Payment/buy-course";
                var res = await _httpClient.PostAsJsonAsync(api, request);

                if (res.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Purchase successful!";
                    return RedirectToPage("/UserCourse");
                }

                var errorContent = await res.Content.ReadAsStringAsync();

                if (errorContent.Contains("NotEnoughPoint", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "You do not have enough points. Please recharge.";
                    return RedirectToPage("/Transaction/Index");
                }

                ModelState.AddModelError("", "Purchase failed. Please try again.");
                return RedirectToPage("/UserCourse");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during course purchase");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return Page();
            }
        }
    }
}