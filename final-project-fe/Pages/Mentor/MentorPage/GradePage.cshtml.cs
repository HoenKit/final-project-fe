using final_project_fe.Dtos;
using final_project_fe.Dtos.Assignment;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using static final_project_fe.Dtos.Lesson.QuizDto;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class GradePageModel : PageModel
    {
        private readonly ILogger<GradePageModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly IHttpClientFactory _httpClientFactory;

        public GradePageModel(ILogger<GradePageModel> logger, IOptions<ApiSettings> apiSettings, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClientFactory = httpClientFactory;
        }
        public List<CourseWithAssignmentsDto> CoursesWithAssignments { get; set; } = new();
        public string? UserId { get; set; }
        public string BaseUrl { get; set; }
        public string? AccessToken { get; set; }
        public List<ListCourseDto> Courses { get; set; } = new();
        public List<SubmissionDto> Submissions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? SelectedAssignmentId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var token = Request.Cookies["AccessToken"];
            AccessToken = token;
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Login"); 

            await LoadAssignmentsAsync();

            if (SelectedAssignmentId.HasValue)
            {
                await LoadSubmissionsAsync(SelectedAssignmentId.Value);
            }

            return Page(); 
        }

        private async Task LoadAssignmentsAsync()
        {
            BaseUrl = _apiSettings.BaseUrl;
            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Please Login First";
                Response.Redirect("/Login");
                return;
            }

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

            if (userIdClaim == null)
                return;

            UserId = userIdClaim.Value;
            var roleClaims = jwtToken.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            // 🔹 Nếu không có role Mentor thì chặn
            if (roleClaims == null || !roleClaims.Any(r => r.Equals("Mentor", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["ErrorMessage"] = "You do not have access to this page!";
                RedirectToPage("/ErrorPage");
                return;
            }
            var client = _httpClientFactory.CreateClient();
            var mentorUrl = $"{BaseUrl}/Mentor/get-by-user/{UserId}";
            var mentorRequest = new HttpRequestMessage(HttpMethod.Get, mentorUrl);
            mentorRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var mentorResponse = await client.SendAsync(mentorRequest);
            if (mentorResponse.IsSuccessStatusCode)
            {
                var mentorResp = await mentorResponse.Content.ReadFromJsonAsync<GetMentorDto>();

                if (mentorResp != null)
                {

                    var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/Course/?mentorId={mentorResp.MentorId}&statuses=Approved");
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var response = await client.SendAsync(request);
                    response.EnsureSuccessStatusCode();

                    var courseResp = await response.Content.ReadFromJsonAsync<PageResult<ListCourseDto>>(
                                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    Courses = courseResp?.Items?
                        .Where(c => c.Assignment != null && !string.IsNullOrEmpty(c.Assignment.MeetLink))
                        .Select(c => new ListCourseDto
                        {
                            CourseId = c.CourseId,
                            CourseName = c.CourseName,
                            Assignment = c.Assignment
                        })
                        .ToList() ?? new List<ListCourseDto>();
                }
            }

        }

        private async Task LoadSubmissionsAsync(int assignmentId)
        {
            BaseUrl = _apiSettings.BaseUrl;
            var token = Request.Cookies["AccessToken"];
            AccessToken = token;
            if (string.IsNullOrEmpty(token)) 
                RedirectToPage("/Login") ;

            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/Learning/submissions?assignmentId={assignmentId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.SendAsync(request);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                Submissions = JsonSerializer.Deserialize<List<SubmissionDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
        }
    }
}