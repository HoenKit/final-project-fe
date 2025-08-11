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
                return RedirectToPage("/Login"); // hoặc xử lý khác

            // 🔓 Giải mã token và lấy userId từ claim
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            var userIdClaim = jsonToken?.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            UserId = userIdClaim;
            string apiUrl = $"{_apiSettings.BaseUrl}/Progress/get-module-progress-by-course?userId={userIdClaim}&courseId={CourseId}";
            var response = await _httpClient.GetFromJsonAsync<List<ModuleProgressDto>>(apiUrl);

            var courseResponse = await _httpClient.GetAsync($"{_apiSettings.BaseUrl}/Course/{CourseId}");
            if (courseResponse.IsSuccessStatusCode)
            {
                var courseJson = await courseResponse.Content.ReadAsStringAsync();
                Course = JsonConvert.DeserializeObject<CourseResponseDto>(courseJson);
            }

            var mentorResponse = await _httpClient.GetAsync($"{_apiSettings.BaseUrl}/Mentor/by-course/{CourseId}");
            if (mentorResponse.IsSuccessStatusCode)
            {
                var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                var mentor = JsonConvert.DeserializeObject<MentorDto>(mentorJson);
                MentorFullName = $"{mentor.FirstName} {mentor.LastName}";
                MentorUserId = mentor.UserId;
            }


            if (response != null)
                Modules = response;
            return Page();
        }

        public async Task<IActionResult> OnGetLessonAsync(int lessonId)
        {
            try
            {
                // Lấy thông tin lesson đầu tiên để kiểm tra có quiz và assignment không
                var lessonRes = await _httpClient.GetAsync($"{_apiSettings.BaseUrl}/Lesson/{lessonId}");
                if (lessonRes.IsSuccessStatusCode)
                {
                    var json = await lessonRes.Content.ReadAsStringAsync();
                    LessonDetail = JsonConvert.DeserializeObject<LessonDetailDto>(json);

                    // Kiểm tra có quiz không
                    var quizRes = await _httpClient.GetAsync($"{_apiSettings.BaseUrl}/Learning/{lessonId}");
                    List<QuestionDto> quiz = null;

                    if (quizRes.IsSuccessStatusCode)
                    {
                        var quizJson = await quizRes.Content.ReadAsStringAsync();
                        quiz = JsonConvert.DeserializeObject<List<QuestionDto>>(quizJson);
                    }

                    // Kiểm tra assignment thông qua API khác
                    bool hasAssignment = false;
                    int? assignmentId = null;

                    try
                    {
                        var assignmentRes = await _httpClient.GetAsync($"{_apiSettings.BaseUrl}/Assignment/get-all-assignment-by-lesson/{lessonId}");
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
                        // Nếu lỗi khi gọi API assignment, coi như không có
                        hasAssignment = false;
                    }

                    // Tạo response object với đầy đủ thông tin
                    var responseData = new
                    {
                        lesson = LessonDetail,
                        quiz = quiz?.Any() == true ? quiz : null,
                        hasQuiz = quiz?.Any() == true,
                        hasAssignment = hasAssignment,
                        assignmentId = assignmentId
                    };

                    return new JsonResult(responseData);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching lesson data");
                return StatusCode(500, "Error loading lesson");
            }

            return NotFound();
        }



        public async Task<IActionResult> OnPostCompleteLessonAsync([FromBody] LessonCompletionDto model)
        {
            model.Mark = 100;
            model.IsPassed = true;
            var client = _httpClientFactory.CreateClient();
            var json = JsonConvert.SerializeObject(model);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync($"{_apiSettings.BaseUrl}/Learning/complete-lesson", content);

            if (!response.IsSuccessStatusCode)
                return new JsonResult(new { success = false, error = await response.Content.ReadAsStringAsync() });

            return new JsonResult(new { success = true });
        }

        public async Task<IActionResult> OnPostSubmitQuizAsync()
        {
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
