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
        public PageResult<CategoryDto> Categories { get; set; }
        [BindProperty]
        public PageResult<GetCourseDto> Courses { get; set; }
        public int currentPage { get; set; }
        public string CurrentUserId { get; set; }
        public string BaseUrl { get; set; }
        public List<string> UserRoles { get; private set; }
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";

        public async Task<IActionResult> OnGetAsync(
     int? currentPage,
     int? categoryId,
     string? title,
     string? sortOption,
     bool filterByUser = false)
        {
            BaseUrl = _apiSettings.BaseUrl;

            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                    UserRoles = jsonToken.Claims
                        .Where(c => c.Type == ClaimTypes.Role)
                        .Select(c => c.Value)
                        .ToList();
                }

                if (UserRoles == null || !UserRoles.Contains("Mentor"))
                    return RedirectToPage("/Index");

                var mentorResponse = await _httpClient.GetAsync($"{BaseUrl}/Mentor/get-by-user/{CurrentUserId}");
                if (!mentorResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Can not get Mentor.");
                    ModelState.AddModelError("", "You are not Mentor");
                    return Page();
                }

                var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                var mentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (mentor == null)
                {
                    ModelState.AddModelError("", "Mentor does not exist.");
                    return Page();
                }

                // Get Categories
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
                courseQuery["page"] = (currentPage ?? 1).ToString();
                courseQuery["pageSize"] = "6";
                courseQuery["mentorId"] = (mentor.MentorId).ToString();

                if (!string.IsNullOrWhiteSpace(title))
                    courseQuery["title"] = title;

                if (categoryId.HasValue)
                    courseQuery["categoryId"] = categoryId.Value.ToString();

                if (!string.IsNullOrWhiteSpace(sortOption))
                    courseQuery["sortOption"] = sortOption;

                // Chỉ thêm UserId nếu filterByUser == true
                if (filterByUser && !string.IsNullOrWhiteSpace(CurrentUserId))
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
