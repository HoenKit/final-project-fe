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
using final_project_fe.Dtos.Question;
using System.Reflection;
using final_project_fe.Dtos.Answer;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        [BindProperty]
        public string Topic { get; set; } = string.Empty;

        [BindProperty]
        public int LessonId { get; set; }

        [BindProperty]
        public int Number { get; set; }


        [BindProperty]
        public QuestionResponseDto Question { get; set; } = new QuestionResponseDto();
        public List<QuestionResponseDto> Questions { get; set; } = new();

        [BindProperty]
        public AnswerResponseDto Answer { get; set; } = new AnswerResponseDto();
        public List<AnswerResponseDto> Answers { get; set; } = new();
        public List<ModuleWithLessonsDto> Modules { get; set; } = new List<ModuleWithLessonsDto>();
        public string CurrentUserId { get; set; }
        public string BaseUrl { get; set; }
        public List<string> UserRoles { get; private set; }
        public IFormFile? NewImage { get; set; }
        public IFormFile? Document { get; set; }
        public IFormFile? Video { get; set; }
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

                // Get Questions for each lesson
                if (Modules != null)
                {
                    foreach (var module in Modules)
                    {
                        if (module.Lessons != null)
                        {
                            foreach (var lesson in module.Lessons)
                            {
                                var questionResponse = await _httpClient.GetAsync($"{BaseUrl}/Question/get-all-question-by-lesson/{lesson.LessonId}");
                                if (questionResponse.IsSuccessStatusCode)
                                {
                                    var questionJson = await questionResponse.Content.ReadAsStringAsync();
                                    var questions = JsonSerializer.Deserialize<List<QuestionResponseDto>>(questionJson, new JsonSerializerOptions
                                    {
                                        PropertyNameCaseInsensitive = true
                                    });

                                    if (questions != null)
                                    {
                                        lesson.Questions = questions;

                                        // Get Answers for each question
                                        foreach (var question in questions)
                                        {
                                            var answerResponse = await _httpClient.GetAsync($"{BaseUrl}/Answer/get-all-answer-by-question/{question.QuestionId}");
                                            if (answerResponse.IsSuccessStatusCode)
                                            {
                                                var answerJson = await answerResponse.Content.ReadAsStringAsync();
                                                var answers = JsonSerializer.Deserialize<List<AnswerResponseDto>>(answerJson, new JsonSerializerOptions
                                                {
                                                    PropertyNameCaseInsensitive = true
                                                });

                                                if (answers != null)
                                                {
                                                    question.Answers = answers;
                                                }
                                            }
                                            else
                                            {
                                                _logger.LogWarning($"Could not load answers for question {question.QuestionId}. Status: {answerResponse.StatusCode}");
                                                question.Answers = new(); // Khởi tạo trống để tránh null
                                            }
                                        }
                                    }
                                }
                                else
                                {
                                    _logger.LogWarning($"Could not load questions for lesson {lesson.LessonId}. Status: {questionResponse.StatusCode}");
                                    lesson.Questions = new(); // Khởi tạo trống để tránh null
                                }
                            }
                        }
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



        //Handler Create Module and Lesson by AI
        public async Task<IActionResult> OnPostGenerateModuleByAIAsync(int courseId)
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

                var formContent = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("courseId", courseId.ToString())});
                var response = await _httpClient.PostAsync($"{BaseUrl}/Course/generate-structure/{courseId}", formContent);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Course structure generated successfully.";
                    return RedirectToPage(new { courseId = Module.CourseId });
                }
                else
                {
                    TempData["ErrorMessage"] = "Could not generate modules and lessons for this course.";
                    return RedirectToPage(new { courseId = Module.CourseId });
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error generating course structure for course {courseId}");
                TempData["ErrorMessage"] = "An error occurred while generating course structure.";
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
                form.Add(new StringContent(Lesson.Description ?? ""), "Description");
                form.Add(new StringContent(Lesson.ModuleId.ToString()), "ModuleId");

                if (Document != null)
                {
                    var documentContent = new StreamContent(Document.OpenReadStream());
                    documentContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Document.ContentType);
                    form.Add(documentContent, "Document", Document.FileName);
                }
                if (Video != null)
                {
                    var videoContent = new StreamContent(Video.OpenReadStream());
                    videoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Video.ContentType);
                    form.Add(videoContent, "Video", Video.FileName);
                }

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
                form.Add(new StringContent(Lesson.Description ?? ""), "Description");
                form.Add(new StringContent(Lesson.ModuleId.ToString()), "ModuleId");
                form.Add(new StringContent(Lesson.LessonId.ToString()), "LessonId");

                if (Document != null)
                {
                    var documentContent = new StreamContent(Document.OpenReadStream());
                    documentContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Document.ContentType);
                    form.Add(documentContent, "Document", Document.FileName);
                }
                if (Video != null)
                {
                    var videoContent = new StreamContent(Video.OpenReadStream());
                    videoContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(Video.ContentType);
                    form.Add(videoContent, "Video", Video.FileName);
                }

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



        //Handler Create Question
        public async Task<IActionResult> OnPostAddQuestionAsync()
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
                    JsonSerializer.Serialize(Question),
                    Encoding.UTF8,
                    "application/json"
                );

                // Gọi API tạo Question
                var response = await _httpClient.PostAsync($"{BaseUrl}/Question", jsonContent);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var createdQuestion = JsonSerializer.Deserialize<QuestionResponseDto>(responseContent, options);

                    TempData["SuccessMessage"] = "Question created successfully!";
                    return RedirectToPage(new { courseId = Module.CourseId });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Create question failed! Status: " + response.StatusCode);
                    TempData["ErrorMessage"] = "Failed to create question: " + errorContent;
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while creating question");
                TempData["ErrorMessage"] = "An error occurred while creating the question: " + ex.Message;
                return Page();
            }
        }

        //Handler Update Question
        public async Task<IActionResult> OnPostEditQuestionAsync()
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
                    JsonSerializer.Serialize(Question),
                    Encoding.UTF8,
                    "application/json"
                );

                // Gọi API tạo Question
                var response = await _httpClient.PutAsync($"{BaseUrl}/Question", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var updateQuestion = JsonSerializer.Deserialize<QuestionResponseDto>(responseContent, options);

                    TempData["SuccessMessage"] = "Question update successfully!";
                    return RedirectToPage(new { courseId = Module.CourseId }); // Redirect back to current page
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Update Question failed! Status: " + response.StatusCode);
                    TempData["ErrorMessage"] = "Failed to update question: " + errorContent;
                    return RedirectToPage(new { courseId = Module.CourseId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while update question");
                TempData["ErrorMessage"] = "An error occurred while update the question: " + ex.Message;
                return Page();
            }
        }

        //Handler Delete Question
        public async Task<IActionResult> OnPostDeleteQuestionAsync(int id)
        {
            BaseUrl = _apiSettings.BaseUrl;

            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.DeleteAsync($"{BaseUrl}/Question/{id}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Question deleted successfully.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to delete question. Status: {response.StatusCode}, Error: {error}");
                    TempData["ErrorMessage"] = "Failed to delete question.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while deleting question.");
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the question.";
            }

            // Redirect lại trang hiện tại và giữ lại courseId nếu có
            return RedirectToPage(new { courseId = Module.CourseId });
        }

        //Handler Generate Question and Answer by Upload Excel
        public async Task<IActionResult> OnPostUploadExcel(IFormFile excelFile, int lessonId)
        {
            BaseUrl = _apiSettings.BaseUrl;
            // Validate input
            if (excelFile == null || excelFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select Excel file to upload.";
                return RedirectToPage(new { courseId = Module.CourseId });
            }

            // Validate file extension
            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var fileExtension = Path.GetExtension(excelFile.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                TempData["ErrorMessage"] = "Only Excel files (.xlsx, .xls) are accepted.";
                return RedirectToPage(new { courseId = Module.CourseId });
            }

            // Validate file size (limit 10MB)
            if (excelFile.Length > 10 * 1024 * 1024)
            {
                TempData["ErrorMessage"] = "File must not exceed 10MB.";
                return RedirectToPage(new { courseId = Module.CourseId });
            }

            // Validate lessonId
            if (lessonId <= 0)
            {
                TempData["ErrorMessage"] = "Invalid lesson ID.";
                return Page();
            }

            try
            {
                using var httpClient = new HttpClient();

                // Prepare multipart form data
                using var formData = new MultipartFormDataContent();

                // Add file to form data
                using var fileStream = excelFile.OpenReadStream();
                using var streamContent = new StreamContent(fileStream);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
                formData.Add(streamContent, "file", excelFile.FileName);

                // Add lessonId to form data
                formData.Add(new StringContent(lessonId.ToString()), "lessonId");
                
                // Call API endpoint
                var response = await _httpClient.PostAsync($"{BaseUrl}/Question/upload-excel", formData);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Import Questions Success";
                    return RedirectToPage(new { courseId = Module.CourseId });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API call failed with status: {StatusCode}, Content: {Content}",
                        response.StatusCode, errorContent);

                    TempData["ErrorMessage"] = "An error occurred while importing the Excel file. Please try again.";
                    return RedirectToPage(new { courseId = Module.CourseId });
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request error when calling import API");
                TempData["ErrorMessage"] = "Unable to connect to server. Please try again.";
                return RedirectToPage(new { courseId = Module.CourseId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading Excel file: {FileName}", excelFile.FileName);
                TempData["ErrorMessage"] = "An error occurred while uploading the file. Please try again.";
                return RedirectToPage(new { courseId = Module.CourseId });
            }
        }

        //Handler Generate Question and Answer by AI
        public async Task<IActionResult> OnPostGenerateAIAsync()
        {
            BaseUrl = _apiSettings.BaseUrl;
            // Validate input
            if (string.IsNullOrWhiteSpace(Topic))
            {
                TempData["ErrorMessage"] = "Topic cannot be left blank.";
                return RedirectToPage(new { courseId = Module.CourseId });
            }

            if (LessonId <= 0)
            {
                TempData["ErrorMessage"] = "LessonId is invalid.";
                return RedirectToPage(new { courseId = Module.CourseId });
            }

            if (Number <= 0)
            {
                TempData["ErrorMessage"] = "Number of questions must be greater than 0.";
                return RedirectToPage(new { courseId = Module.CourseId });
            }

            try
            {
                // Tạo request object
                var request = new QuizImportRequest
                {
                    Topic = Topic,
                    LessonId = LessonId,
                    Number = Number
                };

                // Gọi API Backend
                var json = JsonSerializer.Serialize(request);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BaseUrl}/Question/import-AI", content);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<dynamic>(responseContent);

                    TempData["SuccessMessage"] = "Quiz successfully generated from AI!";

                    // Redirect về trang danh sách câu hỏi hoặc trang hiện tại
                    return RedirectToPage(new { courseId = Module.CourseId });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    var errorResult = JsonSerializer.Deserialize<JsonElement>(errorContent);

                    string errorMessage = "An error occurred while generating quiz from AI.";
                    if (errorResult.TryGetProperty("message", out var messageElement))
                    {
                        errorMessage = messageElement.GetString() ?? errorMessage;
                    }

                    TempData["ErrorMessage"] = errorMessage;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger?.LogError(ex, "Network error while calling AI import API");
                TempData["ErrorMessage"] = "Error connecting to server. Please try again later.";
            }
            catch (JsonException ex)
            {
                _logger?.LogError(ex, "JSON serialization error while calling AI import API");
                TempData["ErrorMessage"] = "Error processing data. Please try again later.";
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Unexpected error while calling AI import API");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again later.";
            }

            return Page();
        }



        //Handler Delete Answer
        public async Task<IActionResult> OnPostDeleteAnswerAsync(int id)
        {
            BaseUrl = _apiSettings.BaseUrl;

            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.DeleteAsync($"{BaseUrl}/Answer/{id}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Answer deleted successfully.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to delete answer. Status: {response.StatusCode}, Error: {error}");
                    TempData["ErrorMessage"] = "Failed to delete answer.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while deleting answer.");
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the answer.";
            }

            // Redirect lại trang hiện tại và giữ lại courseId nếu có
            return RedirectToPage(new { courseId = Module.CourseId });
        }
    }
}
