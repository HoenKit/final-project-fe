using final_project_fe.Dtos;
using final_project_fe.Dtos.Assignment;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Payment;
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
    public class AttendancePageModel : PageModel
    {
        private readonly ILogger<AttendancePageModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public AttendancePageModel(ILogger<AttendancePageModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
            _httpClientFactory = httpClientFactory;
        }
        public string BaseUrl { get; set; }
        public List<ListCourseDto> Courses { get; set; } = new();
        public List<UserAssignmentDto> NotPresentUsers { get; set; } = new();

        [BindProperty]
        public int SelectedAssignmentId { get; set; }

        [BindProperty]
        public List<string> SelectedUserIds { get; set; } = new();

        public string? UserId { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadAssignments();
            return Page();
        }

        public async Task<IActionResult> OnPostLoadAsync()
        {
            await LoadAssignments();

            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                ModelState.AddModelError(string.Empty, "Token missing.");
                return Page();
            }

            var client = _httpClientFactory.CreateClient();

            // Tạo HttpRequestMessage thay cho GetAsync
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"{_apiSettings.BaseUrl}/Learning/not-presented?assignmentId={SelectedAssignmentId}"
            );

            // Gắn token vào header
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "Unable to get the list of unchecked attendance.");
                return Page();
            }

            var json = await response.Content.ReadAsStringAsync();
            NotPresentUsers = JsonSerializer.Deserialize<List<UserAssignmentDto>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new();

            return Page();
        }

        public async Task<IActionResult> OnPostMarkAsync()
        {
            await LoadAssignments();

            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                ModelState.AddModelError(string.Empty, "Token missing.");
                return RedirectToPage("/Login");
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var payload = new
            {
                assignmentId = SelectedAssignmentId,
                userIds = SelectedUserIds
            };

            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var response = await client.PutAsync($"{_apiSettings.BaseUrl}/Learning/mark-present", content);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Attendance successful!";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Error when taking attendance: {error}");
            }

            return RedirectToPage(); // reload lại
        }

        private async Task LoadAssignments()
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

                    // Chỉ lấy course có ít nhất 1 assignment có MeetLink
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
    }
}
