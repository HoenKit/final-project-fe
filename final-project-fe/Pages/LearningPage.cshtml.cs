using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Lesson;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Module;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using static final_project_fe.Dtos.Lesson.QuizDto;

namespace final_project_fe.Pages.Mentor
{
    public class LearningPageModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly HttpClient _httpClient;
        private readonly ILogger<LearningPageModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly ImageSettings _imagesettings;
        public LearningPageModel(HttpClient httpClient, IOptions<ApiSettings> apiSettings, ILogger<LearningPageModel> logger, IHttpClientFactory httpClientFactory, IOptions<ImageSettings> imageSettings)
        {
            _httpClient = httpClient;
            _apiSettings = apiSettings.Value;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _imagesettings = imageSettings.Value;
        }

        public CourseResponseDto Course { get; set; }
        public string MentorFullName { get; set; }
        public Guid MentorUserId { get; set; }
        public string BaseUrl { get; set; }
        public string ImageKey { get; set; }
        public List<ModuleProgressDto> Modules { get; set; } = new();
        public List<LessonbyModuleDto> Lessons { get; set; }

        [BindProperty(SupportsGet = true)]
        public string UserId { get; set; }
        [BindProperty]
        public LessonSubmitDto LessonSubmit { get; set; }
        // For lesson popup
        public List<QuestionDto> QuizQuestions { get; set; }
        public LessonDetailDto LessonDetail { get; set; }

        public async Task<IActionResult> OnGetAsync(int CourseId)
        {
            ImageKey = _imagesettings.ImageKey;
            BaseUrl = _apiSettings.BaseUrl;

            string token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            // 🔓 Giải mã token và lấy userId từ claim
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            UserId = jsonToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(UserId))
                return RedirectToPage("/Login");

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1️⃣ Lấy tiến độ module
                var progressRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"{BaseUrl}/Progress/get-module-progress-by-course?userId={UserId}&courseId={CourseId}");
                progressRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var progressResponse = await client.SendAsync(progressRequest);
                if (progressResponse.IsSuccessStatusCode)
                {
                    var progressJson = await progressResponse.Content.ReadAsStringAsync();
                    Modules = JsonConvert.DeserializeObject<List<ModuleProgressDto>>(progressJson);
                }

                // 2️⃣ Lấy thông tin khóa học
                var courseRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"{BaseUrl}/Course/{CourseId}");
                courseRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var courseResponse = await client.SendAsync(courseRequest);
                if (courseResponse.IsSuccessStatusCode)
                {
                    var courseJson = await courseResponse.Content.ReadAsStringAsync();
                    Course = JsonConvert.DeserializeObject<CourseResponseDto>(courseJson);
                }

                // 3️⃣ Lấy thông tin mentor
                var mentorRequest = new HttpRequestMessage(HttpMethod.Get,
                    $"{BaseUrl}/Mentor/by-course/{CourseId}");
                mentorRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var mentorResponse = await client.SendAsync(mentorRequest);
                if (mentorResponse.IsSuccessStatusCode)
                {
                    var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                    var mentor = JsonConvert.DeserializeObject<MentorDto>(mentorJson);
                    if (mentor != null)
                    {
                        MentorFullName = $"{mentor.FirstName} {mentor.LastName}";
                        MentorUserId = mentor.UserId;
                    }
                }

                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading course or modules");
                TempData["ErrorMessage"] = "An error occurred while loading course data.";
                return Page();
            }
        }

        public async Task<IActionResult> OnGetLessonAsync(int lessonId)
        {
            string token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return Unauthorized(); // hoặc RedirectToPage("/Login")

            var client = _httpClientFactory.CreateClient();

            try
            {
                // 1️⃣ Lấy thông tin lesson
                var lessonRequest = new HttpRequestMessage(HttpMethod.Get, $"{_apiSettings.BaseUrl}/Lesson/{lessonId}");
                lessonRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var lessonRes = await client.SendAsync(lessonRequest);
                if (!lessonRes.IsSuccessStatusCode)
                    return NotFound("Lesson not found");

                var lessonJson = await lessonRes.Content.ReadAsStringAsync();
                LessonDetail = JsonConvert.DeserializeObject<LessonDetailDto>(lessonJson);

                // 2️⃣ Kiểm tra quiz
                List<QuestionDto> quiz = null;
                var quizRequest = new HttpRequestMessage(HttpMethod.Get, $"{_apiSettings.BaseUrl}/Learning/{lessonId}");
                quizRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var quizRes = await client.SendAsync(quizRequest);
                if (quizRes.IsSuccessStatusCode)
                {
                    var quizJson = await quizRes.Content.ReadAsStringAsync();
                    quiz = JsonConvert.DeserializeObject<List<QuestionDto>>(quizJson);
                }

                // 3️⃣ Kiểm tra assignment
                bool hasAssignment = false;
                int? assignmentId = null;

                try
                {
                    var assignmentRequest = new HttpRequestMessage(HttpMethod.Get,
                        $"{_apiSettings.BaseUrl}/Assignment/get-all-assignment-by-lesson/{lessonId}");
                    assignmentRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var assignmentRes = await client.SendAsync(assignmentRequest);
                    if (assignmentRes.IsSuccessStatusCode)
                    {
                        var assignmentJson = await assignmentRes.Content.ReadAsStringAsync();
                        var assignmentArray = JsonConvert.DeserializeObject<JArray>(assignmentJson);
                        if (assignmentArray != null && assignmentArray.Any())
                        {
                            var firstAssignment = assignmentArray.First;
                            hasAssignment = true;
                            assignmentId =
                                (int?)firstAssignment["assignmentId"] ??
                                (int?)firstAssignment["AssignmentId"] ??
                                (int?)firstAssignment["id"] ??
                                (int?)firstAssignment["Id"];
                        }
                    }
                }
                catch
                {
                    hasAssignment = false;
                }

                // 4️⃣ Trả về dữ liệu
                var responseData = new
                {
                    lesson = LessonDetail,
                    quiz = quiz?.Any() == true ? quiz : null,
                    hasQuiz = quiz?.Any() == true,
                    hasAssignment,
                    assignmentId
                };

                return new JsonResult(responseData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching lesson data");
                return StatusCode(500, "Error loading lesson");
            }
        }




        public async Task<IActionResult> OnPostCompleteLessonAsync([FromBody] LessonCompletionDto model)
        {
            string token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return Unauthorized(); // Hoặc RedirectToPage("/Login")

            // Đánh dấu bài học hoàn thành
            model.Mark = 100;
            model.IsPassed = true;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var json = JsonConvert.SerializeObject(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{_apiSettings.BaseUrl}/Learning/complete-lesson", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                return new JsonResult(new { success = false, error = errorMsg });
            }

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostSubmitQuizAsync()
        {
            string token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return Unauthorized(); // Hoặc RedirectToPage("/Login")

            using var reader = new StreamReader(Request.Body);
            var body = await reader.ReadToEndAsync();

            LessonSubmitDto model;
            try
            {
                model = JsonConvert.DeserializeObject<LessonSubmitDto>(body);
            }
            catch (Exception ex)
            {
                return new JsonResult(new { success = false, error = "Invalid JSON format", details = ex.Message });
            }

            if (model == null)
            {
                return new JsonResult(new { success = false, error = "Model is null" });
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var json = JsonConvert.SerializeObject(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{_apiSettings.BaseUrl}/Learning/submit", content);

            if (!response.IsSuccessStatusCode)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                return new JsonResult(new { success = false, error = errorMsg });
            }

            var responseJson = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(responseJson);

            return new JsonResult(new
            {
                success = true,
                score = (int)result.score,
                isPassed = (bool)result.isPassed
            });
        }
      

    }

}
