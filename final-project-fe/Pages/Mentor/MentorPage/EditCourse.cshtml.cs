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
using System.Text;
using System.Net.Http.Json;

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

        [BindProperty]
        public ModuleResponseDto Module { get; set; } = new ModuleResponseDto();

        [BindProperty]
        public UpdateLessonDto Lesson { get; set; } = new UpdateLessonDto();

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
                    return RedirectToPage("/Mentor/MentorPage/MyCourses");
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
                    return RedirectToPage("/Mentor/MentorPage/MyCourses");
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
                form.Add(new StringContent(Course.IntendedLearner ?? ""), "IntendedLearner");
                form.Add(new StringContent(Course.Language ?? ""), "Language");
                form.Add(new StringContent(Course.Level ?? ""), "Level");
                form.Add(new StringContent(Course.Requirement ?? ""), "Requirement");
                form.Add(new StringContent(mentor.MentorId.ToString()), "MentorId");

                if (NewImage != null)
                {
                    var stream = NewImage.OpenReadStream();
                    var fileContent = new StreamContent(stream);
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(NewImage.ContentType);
                    form.Add(fileContent, "CoursesImage", NewImage.FileName);
                }

                var response = await _httpClient.PutAsync($"{BaseUrl}/Course", form);

                // DEBUG: Log detailed response information
                _logger.LogInformation($"Update Course Response Status: {response.StatusCode}");
                _logger.LogInformation($"Update Course IsSuccessStatusCode: {response.IsSuccessStatusCode}");

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


        // Handler Create Module
        public async Task<IActionResult> OnPostAddModuleAsync()
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

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                // Gọi API lấy thông tin mentor
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

                // Lấy dữ liệu từ form
                /* var moduleDto = new ModuleDto
                 {
                     CourseId = int.Parse(Request.Form["courseId"].ToString()),
                     Title = Request.Form["title"].ToString(),
                     Description = Request.Form["description"].ToString() ?? "",
                     IsPremium = Request.Form["isPremium"].ToString().Contains("true") ||
                                Request.Form["isPremium"].ToString().Contains("on"),
                 };*/            
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(Module),
                    Encoding.UTF8,
                    "application/json"
                );

                // Gọi API tạo module
                var response = await _httpClient.PostAsync($"{BaseUrl}/Module", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var createdModule = JsonSerializer.Deserialize<ModuleResponseDto>(responseContent, options);

                    TempData["SuccessMessage"] = "Module created successfully!";
                    return RedirectToPage(new { courseId = Module.CourseId }); // Redirect back to current page
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Create Module failed! Status: " + response.StatusCode);
                    TempData["ErrorMessage"] = "Failed to create module: " + errorContent;
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating module");
                TempData["ErrorMessage"] = "An error occurred while creating the module: " + ex.Message;
                return Page();
            }
        }

        // Handler Update Module
        public async Task<IActionResult> OnPostEditModuleAsync()
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

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi API lấy thông tin mentor
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
                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(Module),
                    Encoding.UTF8,
                    "application/json"
                );

                // Gọi API tạo module
                var response = await _httpClient.PutAsync($"{BaseUrl}/Module", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var updateModule = JsonSerializer.Deserialize<ModuleResponseDto>(responseContent, options);

                    TempData["SuccessMessage"] = "Module update successfully!";
                    return RedirectToPage(new { courseId = Module.CourseId }); // Redirect back to current page
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Update Module failed! Status: " + response.StatusCode);
                    TempData["ErrorMessage"] = "Failed to update module: " + errorContent;
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while update module");
                TempData["ErrorMessage"] = "An error occurred while update the module: " + ex.Message;
                return Page();
            }
        }

        //Handler Delete Module
        public async Task<IActionResult> OnPostDeleteModuleAsync(int id)
        {
            BaseUrl = _apiSettings.BaseUrl;

            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.DeleteAsync($"{BaseUrl}/Module/{id}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Module deleted successfully.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to delete module. Status: {response.StatusCode}, Error: {error}");
                    TempData["ErrorMessage"] = "Failed to delete module.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while deleting module.");
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the module.";
            }

            // Redirect lại trang hiện tại và giữ lại courseId nếu có
            return RedirectToPage(new { courseId = Module.CourseId });
        }
        


        //Handler Create Lesson
        public async Task<IActionResult> OnPostAddLessonAsync()
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

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi API lấy thông tin mentor
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
                form.Add(new StringContent(Lesson.Title ?? ""), "Title");
                /*form.Add(new StringContent(Lesson.Description ?? ""), "Description");*/
                form.Add(new StringContent(Lesson.ModuleId.ToString()), "ModuleId");

                // Gọi API tạo Lesson
                var response = await _httpClient.PostAsync($"{BaseUrl}/Lesson", form);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var createdLesson = JsonSerializer.Deserialize<UpdateLessonDto>(responseContent, options);

                    TempData["SuccessMessage"] = "Lesson created successfully!";
                    return RedirectToPage(new { courseId = Module.CourseId }); // Redirect back to current page
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Create lesson failed! Status: " + response.StatusCode);
                    TempData["ErrorMessage"] = "Failed to create lesson: " + errorContent;
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating lesson");
                TempData["ErrorMessage"] = "An error occurred while creating the lesson: " + ex.Message;
                return Page();
            }
        }

        //Handler Update Lesson
        public async Task<IActionResult> OnPostEditLessonAsync()
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

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi API lấy thông tin mentor
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
                form.Add(new StringContent(Lesson.Title ?? ""), "Title");
                /*form.Add(new StringContent(Lesson.Description ?? ""), "Description");*/
                form.Add(new StringContent(Lesson.ModuleId.ToString()), "ModuleId");
                form.Add(new StringContent(Lesson.LessonId.ToString()), "LessonId");

                // Gọi API tạo Lesson
                var response = await _httpClient.PutAsync($"{BaseUrl}/Lesson", form);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var updateLesson = JsonSerializer.Deserialize<UpdateLessonDto>(responseContent, options);

                    TempData["SuccessMessage"] = "Lesson update successfully!";
                    return RedirectToPage(new { courseId = Module.CourseId }); // Redirect back to current page
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Update lesson failed! Status: " + response.StatusCode);
                    TempData["ErrorMessage"] = "Failed to update lesson: " + errorContent;
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while updating lesson");
                TempData["ErrorMessage"] = "An error occurred while updating the lesson: " + ex.Message;
                return Page();
            }
        }

        //Handler Delete Lesson
        public async Task<IActionResult> OnPostDeleteLessonAsync(int id)
        {
            BaseUrl = _apiSettings.BaseUrl;

            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.DeleteAsync($"{BaseUrl}/Lesson/{id}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Lesson deleted successfully.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to delete Lesson. Status: {response.StatusCode}, Error: {error}");
                    TempData["ErrorMessage"] = "Failed to delete Lesson.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while deleting Lesson.");
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the Lesson.";
            }

            // Redirect lại trang hiện tại và giữ lại courseId nếu có
            return RedirectToPage(new { courseId = Module.CourseId });
        }
    }
}
