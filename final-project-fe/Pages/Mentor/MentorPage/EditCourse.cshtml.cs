using final_project_fe.Dtos.Category;
using final_project_fe.Dtos;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Lesson;
using final_project_fe.Dtos.Module;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Web;
using System.Net.Http.Headers;
using final_project_fe.Dtos.Mentors;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class EditCourseModel : PageModel
    {
        private readonly ILogger<EditCourseModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        public EditCourseModel(ILogger<EditCourseModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }
        [BindProperty]
        public CourseResponseDto Course { get; set; } = new CourseResponseDto();
        public List<ModuleWithLessonsDto> Modules { get; set; } = new List<ModuleWithLessonsDto>();
        public string CurrentUserId { get; set; }
        public string BaseUrl { get; set; }
        public List<string> UserRoles { get; private set; }
        public IFormFile? NewImage { get; set; }
        public PageResult<CategoryDto> Categories { get; set; } = new(new List<CategoryDto>(), 0, 1, 10);
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";
        public async Task<IActionResult> OnGetAsync(int courseId)
        {
            BaseUrl = _apiSettings.BaseUrl;
            try
            {
                // Authen, Author
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

                // Get Category
                var categoryUrl = new UriBuilder($"{BaseUrl}/Category");
                var categoryQuery = HttpUtility.ParseQueryString(string.Empty);
                categoryQuery["page"] = "1";
                categoryQuery["pageSize"] = "100";
                categoryUrl.Query = categoryQuery.ToString();

                var cateResponse = await _httpClient.GetAsync(categoryUrl.ToString());
                if (cateResponse.IsSuccessStatusCode)
                {
                    var categoryJson = await cateResponse.Content.ReadAsStringAsync();
                    Categories = JsonSerializer.Deserialize<PageResult<CategoryDto>>(categoryJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);
                }
                else
                {
                    _logger.LogWarning("Không thể lấy danh mục. Status: " + cateResponse.StatusCode);
                }

                // Get Course
                var courseResponse = await _httpClient.GetAsync($"{BaseUrl}/Course/{courseId}");
                if (!courseResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Can not get Course. Status: " + courseResponse.StatusCode);
                    TempData["ErrorMessage"] = "Can not get Course!";
                    return RedirectToPage("/Mentor/MentorPage/Index");
                }

                var courseJson = await courseResponse.Content.ReadAsStringAsync();
                Course = JsonSerializer.Deserialize<CourseResponseDto>(courseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (Course == null)
                {
                    ModelState.AddModelError("", "Can not find Course.");
                    TempData["ErrorMessage"] = "Can not find Course!";
                    return RedirectToPage("/Mentor/MentorPage/Index");
                }

                Course.CourseId = courseId;

                if (Course.CoursesImage != null)
                {
                    Course.CoursesImage = ImageUrlHelper.AppendSasTokenIfNeeded(Course.CoursesImage, SasToken);
                }

                // Get Module and Lesson
                var response = await _httpClient.GetAsync($"{BaseUrl}/Module/get-all-module-by-course/{courseId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var modules = JsonSerializer.Deserialize<List<ModuleWithLessonsDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (modules != null)
                    {
                        foreach (var module in modules)
                        {
                            var lessonRes = await _httpClient.GetAsync($"{BaseUrl}/Lesson/get-all-lesson-by-module/{module.ModuleId}");
                            if (lessonRes.IsSuccessStatusCode)
                            {
                                var lessonJson = await lessonRes.Content.ReadAsStringAsync();
                                var lessons = JsonSerializer.Deserialize<List<LessonResponseDto>>(lessonJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                module.Lessons = lessons ?? new();
                            }
                        }

                        Modules = modules;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while edit course");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
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

                var form = new MultipartFormDataContent();

                form.Add(new StringContent(Course.CourseId.ToString()), "CourseId");
                form.Add(new StringContent(Course.CourseName ?? ""), "CourseName");
                form.Add(new StringContent(Course.CourseContent ?? ""), "CourseContent");
                form.Add(new StringContent(Course.Cost.ToString()), "Cost");
                form.Add(new StringContent(Course.SkillLearn ?? ""), "SkillLearn");
                form.Add(new StringContent(Course.CourseLength?.ToString() ?? ""), "CourseLength");
                form.Add(new StringContent(Course.CategoryId.ToString()), "CategoryId");
                form.Add(new StringContent(mentor.MentorId.ToString()), "MentorId");

                if (NewImage != null)
                {
                    var stream = NewImage.OpenReadStream();
                    var fileContent = new StreamContent(stream);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(NewImage.ContentType);
                    form.Add(fileContent, "CoursesImage", NewImage.FileName);
                }

                var response = await _httpClient.PutAsync($"{BaseUrl}/Course", form);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Update course successfully!";
                    return await OnGetAsync(Course.CourseId);
                }
                else
                {
                    _logger.LogError("Update failed. Status: " + response.StatusCode);
                    ModelState.AddModelError("", "Update failed.");
                    TempData["ErrorMessage"] = "Failed to update course.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating course.");
                ModelState.AddModelError("", "Unexpected error occurred.");
                TempData["ErrorMessage"] = "Unexpected error occurred.";
            }

            return await OnGetAsync(Course.CourseId); 
        }

        public async Task<IActionResult> OnPostToggleDeleteCourseAsync(int id)
        {
            try
            {
                // Authen, Author
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

                //Toogle Delete
                var response = await _httpClient.PutAsync($"{_apiSettings.BaseUrl}/Course/toggle-deleted/{id}", null);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Course deleted.";
                    return RedirectToPage("/Mentor/MentorPage/MyCourses");
                }
                else
                {
                    ModelState.AddModelError("", "Failed to delete course.");
                    TempData["ErrorMessage"] = "Failed to delete course.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while soft deleting course.");
                ModelState.AddModelError("", "Unexpected error occurred.");
                TempData["ErrorMessage"] = "Unexpected error occurred.";
                return Page();
            }
        }

    }
}
