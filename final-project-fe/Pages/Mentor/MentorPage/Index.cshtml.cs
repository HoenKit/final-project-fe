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
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";

        public async Task<IActionResult> OnGetAsync(
    int? currentPage,
    int? categoryId,
    string? title,
    string? sortOption)
        {
            BaseUrl = _apiSettings.BaseUrl;

            try
            {
                // Lấy userId từ token
                string? token = Request.Cookies["AccessToken"];
                var handler = new JwtSecurityTokenHandler();
                if (!string.IsNullOrEmpty(token))
                {
                    var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                    CurrentUserId = jsonToken?.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }
                // Get Category
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
                // Get Course
                var courseUrl = new UriBuilder($"{BaseUrl}/Course");
                var courseQuery = HttpUtility.ParseQueryString(string.Empty);
                courseQuery["page"] = currentPage.ToString();
                courseQuery["pageSize"] = "6";

                if (!string.IsNullOrWhiteSpace(title))
                    courseQuery["title"] = title;

                if (categoryId.HasValue)
                    courseQuery["categoryId"] = categoryId.Value.ToString();

                if (!string.IsNullOrWhiteSpace(sortOption))
                    courseQuery["sortOption"] = sortOption;

                if (!string.IsNullOrWhiteSpace(CurrentUserId))
                    courseQuery["userId"] = CurrentUserId;

                courseUrl.Query = courseQuery.ToString();

                var courseResponse = await _httpClient.GetAsync(courseUrl.ToString());
                if (courseResponse.IsSuccessStatusCode)
                {
                    var courseJson = await courseResponse.Content.ReadAsStringAsync();
                    Courses = JsonSerializer.Deserialize<PageResult<GetCourseDto>>(courseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 10);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi API lấy dữ liệu Course/Category.");
            }

            return Page();
        }
    }
}
