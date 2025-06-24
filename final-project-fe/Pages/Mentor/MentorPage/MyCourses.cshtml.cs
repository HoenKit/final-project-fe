using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using final_project_fe.Dtos.Mentors;
using Microsoft.Extensions.Logging;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class MyCoursesModel : PageModel
    {
        private readonly ILogger<MyCoursesModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public MyCoursesModel(ILogger<MyCoursesModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public PageResult<CategoryDto> Categories { get; set; } = new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);

        [BindProperty]
        public PageResult<GetCourseDto> Courses { get; set; } = new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 6);

        public int CurrentPage { get; set; } = 1;
        public string CurrentUserId { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public List<string> UserRoles { get; private set; } = new List<string>();
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";
        public GetMentorDto? CurrentMentor { get; set; }

        public async Task<IActionResult> OnGetAsync(
            int? currentPage,
            int? categoryId,
            string? title,
            string? sortOption,
            string? language,
            string? level,
            decimal? minCost,
            decimal? maxCost,
            decimal? minRate,
            decimal? maxRate,
            string? status)  // Thêm parameter status
        {
            BaseUrl = _apiSettings.BaseUrl;
            CurrentPage = currentPage ?? 1;

            try
            {
                // Check authentication
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                {
                    TempData["ErrorMessage"] = "Please login to access this page.";
                    return RedirectToPage("/Login");
                }

                // Parse JWT token
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

                    UserRoles = jsonToken.Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value)
                        .ToList();
                }

                // Check if user is mentor
                if (UserRoles == null || !UserRoles.Contains("Mentor"))
                {
                    TempData["ErrorMessage"] = "Access denied. You must be a mentor to view this page.";
                    return RedirectToPage("/Index");
                }

                // Get current mentor info
                var mentorResponse = await _httpClient.GetAsync($"{BaseUrl}/Mentor/get-by-user/{CurrentUserId}");
                if (!mentorResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Cannot get mentor information for user: {UserId}", CurrentUserId);
                    TempData["ErrorMessage"] = "Mentor information not found.";
                    return RedirectToPage("/Index");
                }

                var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                CurrentMentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (CurrentMentor == null)
                {
                    TempData["ErrorMessage"] = "Mentor profile not found.";
                    return RedirectToPage("/Index");
                }

                // Load categories
                await LoadCategoriesAsync();

                // Load courses
                await LoadCoursesAsync(currentPage, categoryId, title, sortOption, language, level, minCost, maxCost, minRate, maxRate, status);

                return Page();
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP error while loading my courses");
                TempData["ErrorMessage"] = "Unable to connect to the server. Please try again later.";
                return Page();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON parsing error while loading my courses");
                TempData["ErrorMessage"] = "Error processing server response.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while loading my courses");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return Page();
            }
        }

        private async Task LoadCategoriesAsync()
        {
            try
            {
                var categoryUrl = new UriBuilder($"{BaseUrl}/Category");
                var categoryQuery = HttpUtility.ParseQueryString(string.Empty);
                categoryQuery["page"] = "1";
                categoryQuery["pageSize"] = "100";
                categoryUrl.Query = categoryQuery.ToString();

                var categoryResponse = await _httpClient.GetAsync(categoryUrl.ToString());
                if (categoryResponse.IsSuccessStatusCode)
                {
                    var categoryJson = await categoryResponse.Content.ReadAsStringAsync();
                    Categories = JsonSerializer.Deserialize<PageResult<CategoryDto>>(categoryJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 100);
                }
                else
                {
                    _logger.LogWarning("Failed to load categories. Status: {StatusCode}", categoryResponse.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories");
            }
        }

        private async Task LoadCoursesAsync(
            int? currentPage,
            int? categoryId,
            string? title,
            string? sortOption,
            string? language,
            string? level,
            decimal? minCost,
            decimal? maxCost,
            decimal? minRate,
            decimal? maxRate,
            string? status)  // Thêm parameter status
        {
            try
            {
                if (CurrentMentor == null) return;

                var courseUrl = new UriBuilder($"{BaseUrl}/Course");
                var courseQuery = HttpUtility.ParseQueryString(string.Empty);

                // Basic pagination
                courseQuery["page"] = (currentPage ?? 1).ToString();
                courseQuery["pageSize"] = "8";

                // Filter by current mentor
                courseQuery["mentorId"] = CurrentMentor.MentorId.ToString();

                // Add optional filters - Đồng bộ với controller parameters
                if (!string.IsNullOrWhiteSpace(title))
                    courseQuery["title"] = title.Trim();

                if (categoryId.HasValue && categoryId.Value > 0)
                    courseQuery["CategoryId"] = categoryId.Value.ToString();  // Dùng CategoryId với C hoa

                if (!string.IsNullOrWhiteSpace(sortOption))
                    courseQuery["sortOption"] = sortOption;

                if (!string.IsNullOrWhiteSpace(language))
                    courseQuery["Language"] = language;  // Dùng Language với L hoa

                if (!string.IsNullOrWhiteSpace(level))
                    courseQuery["Level"] = level;  // Dùng Level với L hoa

                if (minCost.HasValue && minCost.Value >= 0)
                    courseQuery["MinCost"] = minCost.Value.ToString();  // Dùng MinCost với M hoa

                if (maxCost.HasValue && maxCost.Value >= 0)
                    courseQuery["MaxCost"] = maxCost.Value.ToString();  // Dùng MaxCost với M hoa

                if (minRate.HasValue && minRate.Value >= 0)
                    courseQuery["MinRate"] = minRate.Value.ToString();  // Dùng MinRate với M hoa

                if (maxRate.HasValue && maxRate.Value >= 0)
                    courseQuery["MaxRate"] = maxRate.Value.ToString();  // Dùng MaxRate với M hoa

                if (!string.IsNullOrWhiteSpace(status))
                    courseQuery["statuses"] = status;  // Dùng statuses như trong controller

                courseUrl.Query = courseQuery.ToString();

                _logger.LogInformation("Loading courses with URL: {Url}", courseUrl.ToString());

                var courseResponse = await _httpClient.GetAsync(courseUrl.ToString());
                if (courseResponse.IsSuccessStatusCode)
                {
                    var courseJson = await courseResponse.Content.ReadAsStringAsync();
                    Courses = JsonSerializer.Deserialize<PageResult<GetCourseDto>>(courseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 8);

                    // Gắn SAS token vào mỗi course image
                    if (Courses?.Items != null)
                    {
                        foreach (var course in Courses.Items)
                        {
                            if (!string.IsNullOrWhiteSpace(course.CoursesImage))
                            {
                                course.CoursesImage = ImageUrlHelper.AppendSasTokenIfNeeded(course.CoursesImage, SasToken);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to load courses. Status: {StatusCode}, Response: {Response}",
                        courseResponse.StatusCode, await courseResponse.Content.ReadAsStringAsync());

                    Courses = new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 8);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading courses");
                Courses = new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 8);
            }
        }
    }
}