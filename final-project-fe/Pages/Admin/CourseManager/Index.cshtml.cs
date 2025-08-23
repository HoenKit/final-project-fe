using final_project_fe.Dtos;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Courses;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using System.Web;

namespace final_project_fe.Pages.Admin.CourseManager
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
                if (!Request.Cookies.ContainsKey("AccessToken"))
                    return RedirectToPage("/Login");

                string token = Request.Cookies["AccessToken"];
                string? role = JwtHelper.GetRoleFromToken(token);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (role != "Admin")
                    return RedirectToPage("/Index");

                //Lấy trang trước đấy
                const string sessionKey = "PageHistory";
                var history = HttpContext.Session.GetString(sessionKey);
                List<string> pageHistory;

                if (string.IsNullOrEmpty(history))
                {
                    pageHistory = new List<string>();
                }
                else
                {
                    pageHistory = JsonSerializer.Deserialize<List<string>>(history);
                }

                // Lấy URL hiện tại
                var currentUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;

                // Chỉ thêm nếu khác trang cuối cùng
                if (pageHistory.Count == 0 || pageHistory.Last() != currentUrl)
                {
                    pageHistory.Add(currentUrl);
                }

                // Lưu lại vào session
                HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(pageHistory));

                // Load Categories
                await LoadCategories();

                // Load Courses with all filters
                await LoadCourses(currentPage);
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
                string token = Request.Cookies["AccessToken"];
                string? role = JwtHelper.GetRoleFromToken(token);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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

        private async Task LoadCourses(int? currentPage)
        {
            try
            {
                var courseUrl = new UriBuilder($"{BaseUrl}/Course");
                var courseQuery = HttpUtility.ParseQueryString(string.Empty);

                string token = Request.Cookies["AccessToken"];
                string? role = JwtHelper.GetRoleFromToken(token);
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                // Basic parameters
                courseQuery["page"] = (currentPage ?? 1).ToString();
                courseQuery["pageSize"] = "15";
                courseQuery["statuses"] = "Approved";

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
                                course.CoursesImage = ImageUrlHelper.AppendSasTokenIfNeeded(course.CoursesImage, SasToken);
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
    }
}
