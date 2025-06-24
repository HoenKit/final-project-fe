using final_project_fe.Dtos.Post;
using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Category;
using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Cors.Infrastructure;
using System.Text.Json;
using System.Web;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public IndexModel(ILogger<IndexModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public PageResult<CategoryDto> Categories { get; set; }

        [BindProperty]
        public PageResult<GetCourseDto> Courses { get; set; }

        public int currentPage { get; set; }
        public string CurrentUserId { get; set; }
        public string BaseUrl { get; set; }
        public List<string> UserRoles { get; private set; }
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";

        public async Task<IActionResult> OnGetAsync(int? currentPage, int? categoryId, string? title, string? sortOption, string? language, string? level, decimal? minCost, decimal? maxCost, decimal? minRate, decimal? maxRate, int? mentorId, string? categories, string? languages, bool filterByUser = false)
        {
            BaseUrl = _apiSettings.BaseUrl;

            try
            {
                // Get UserId from token if logged in
                string? token = Request.Cookies["AccessToken"];

                if (!string.IsNullOrEmpty(token))
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                    CurrentUserId = jsonToken?.Claims
                                           .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                    UserRoles = jsonToken?.Claims
                           .Where(c => c.Type == ClaimTypes.Role)
                           .Select(c => c.Value)
                           .ToList();
                }

                // Load Categories
                await LoadCategories();

                // Load Courses with all filters
                await LoadCourses(currentPage, categoryId, title, sortOption, filterByUser,
                                language, level, minCost, maxCost, minRate, maxRate, mentorId,
                                categories, languages);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling API to get Course/Category data.");
                // Initialize default data on error
                Categories = new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);
                Courses = new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 10);
            }

            return Page();
        }

        private async Task LoadCategories()
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
                    }) ?? new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);
                }
                else
                {
                    Categories = new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading categories");
                Categories = new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);
            }
        }

        private async Task LoadCourses(int? currentPage, int? categoryId, string? title, string? sortOption,
                                     bool filterByUser, string? language, string? level, decimal? minCost,
                                     decimal? maxCost, decimal? minRate, decimal? maxRate, int? mentorId,
                                     string? categories, string? languages)
        {
            try
            {
                var courseUrl = new UriBuilder($"{BaseUrl}/Course");
                var courseQuery = HttpUtility.ParseQueryString(string.Empty);

                // Basic parameters
                courseQuery["page"] = (currentPage ?? 1).ToString();
                courseQuery["pageSize"] = "15";
                courseQuery["statuses"] = "Approved";

                // Search by title
                if (!string.IsNullOrWhiteSpace(title))
                    courseQuery["title"] = title;

                // Filter by single category
                if (categoryId.HasValue)
                    courseQuery["categoryId"] = categoryId.Value.ToString();

                // Sorting
                if (!string.IsNullOrWhiteSpace(sortOption))
                    courseQuery["sortOption"] = sortOption;

                // Filter by user (only courses of current mentor)
                if (filterByUser && !string.IsNullOrWhiteSpace(CurrentUserId))
                    courseQuery["userId"] = CurrentUserId;

                // Filter by mentorId
                if (mentorId.HasValue)
                    courseQuery["mentorId"] = mentorId.Value.ToString();

                // Filter by single language
                if (!string.IsNullOrWhiteSpace(language))
                    courseQuery["language"] = language;

                // Filter by multiple languages (from checkbox)
                if (!string.IsNullOrWhiteSpace(languages))
                    courseQuery["language"] = languages;

                // Filter by level
                if (!string.IsNullOrWhiteSpace(level))
                    courseQuery["level"] = level;

                // Filter by price
                if (minCost.HasValue)
                    courseQuery["minCost"] = minCost.Value.ToString();

                if (maxCost.HasValue)
                    courseQuery["maxCost"] = maxCost.Value.ToString();

                // Filter by rating
                if (minRate.HasValue)
                    courseQuery["minRate"] = minRate.Value.ToString();

                if (maxRate.HasValue)
                    courseQuery["maxRate"] = maxRate.Value.ToString();

                // Filter by multiple categories (from checkbox)
                if (!string.IsNullOrWhiteSpace(categories))
                {
                    var categoryIds = categories.Split(',');
                    if (categoryIds.Length == 1 && int.TryParse(categoryIds[0], out int singleCategoryId))
                    {
                        courseQuery["categoryId"] = singleCategoryId.ToString();
                    }
                }

                courseUrl.Query = courseQuery.ToString();

                var courseResponse = await _httpClient.GetAsync(courseUrl.ToString());
                if (courseResponse.IsSuccessStatusCode)
                {
                    var courseJson = await courseResponse.Content.ReadAsStringAsync();
                    Courses = JsonSerializer.Deserialize<PageResult<GetCourseDto>>(courseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 15);

                    // Gắn SAS token vào mỗi course image
                    if (Courses?.Items != null)
                    {
                        foreach (var course in Courses.Items)
                        {
                            if (!string.IsNullOrWhiteSpace(course.CoursesImage))
                            {
                                course.CoursesImage =ImageUrlHelper.AppendSasTokenIfNeeded(course.CoursesImage, SasToken);
                            }
                        }
                    }
                }
                else
                {
                    Courses = new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 15);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading courses");
                Courses = new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 15);
            }
        }

        // API endpoint to handle AJAX requests from frontend
        public async Task<IActionResult> OnGetSearchCoursesAsync(
            int page = 1,
            int pageSize = 6,
            string? title = null,
            string? sortOption = null,
            string? categories = null,
            string? languages = null,
            string? level = null,
            decimal? minPrice = null,
            decimal? maxPrice = null,
            decimal? minRating = null,
            decimal? maxRating = null)
        {
            try
            {
                await LoadCourses(page, null, title, sortOption, false,
                                null, level, minPrice, maxPrice, minRating, maxRating,
                                null, categories, languages);

                return new JsonResult(new
                {
                    success = true,
                    courses = Courses.Items, // Thay đổi từ Data thành Items
                    totalRecords = Courses.TotalCount, // Thay đổi từ TotalRecords thành TotalCount
                    currentPage = Courses.CurrentPage,
                    totalPages = Courses.TotalPages,
                    pageSize = Courses.PageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching courses");
                return new JsonResult(new
                {
                    success = false,
                    message = "An error occurred while searching courses"
                });
            }
        }
    }
}