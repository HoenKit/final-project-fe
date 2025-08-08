using final_project_fe.Dtos.Assignment;
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
        public List<GetAssignmentbycreatorDto> Assignments { get; set; } = new();
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
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync($"{_apiSettings.BaseUrl}/Learning/not-presented?assignmentId={SelectedAssignmentId}");

            if (!response.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "Không thể lấy danh sách chưa điểm danh.");
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
                TempData["SuccessMessage"] = "Điểm danh thành công!";
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError(string.Empty, $"Lỗi khi điểm danh: {error}");
            }

            return RedirectToPage(); // reload lại
        }

        private async Task LoadAssignments()
        {
            BaseUrl = _apiSettings.BaseUrl;
            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token)) {
                RedirectToPage("/Login"); 
            }
                

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

            if (userIdClaim == null) return;

            UserId = userIdClaim.Value;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var response = await client.GetAsync($"{BaseUrl}/Assignment/by-creator?userId={UserId}");
                var content = await response.Content.ReadAsStringAsync();
                Assignments = JsonSerializer.Deserialize<List<GetAssignmentbycreatorDto>>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading assignments");
            }
        }
    }
}
