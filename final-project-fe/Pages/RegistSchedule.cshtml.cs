using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Schedule;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;

namespace final_project_fe.Pages
{
    public class RegistScheduleModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<RegistScheduleModel> _logger;
        private readonly ApiSettings _apiSettings;

        public RegistScheduleModel(HttpClient httpClient, ILogger<RegistScheduleModel> logger, IOptions<ApiSettings> apiSettings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiSettings = apiSettings.Value;
        }

        [BindProperty(SupportsGet = true)]
        public int Id { get; set; } // CourseId from route

        public string BaseUrl { get; set; } = string.Empty;
        public MentorDto? Mentor { get; set; }

        public List<ScheduleDto> Schedules { get; set; } = new();
        public List<ScheduleDto> RegisteredSchedules { get; set; } = new();

        public string CurrentUserId { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                BaseUrl = _apiSettings.BaseUrl;

                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                CurrentUserId = jwtToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(CurrentUserId))
                    return RedirectToPage("/Login");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Lấy mentor
                Mentor = await _httpClient.GetFromJsonAsync<MentorDto>($"{BaseUrl}/Mentor/by-course/{Id}");

                // Lấy tất cả schedule theo course
                var scheduleList = await _httpClient.GetFromJsonAsync<List<ScheduleDto>>($"{BaseUrl}/Schedule/by-course/{Id}");

                // Lấy danh sách đã đăng ký của user
                var registered = await _httpClient.GetFromJsonAsync<List<UserScheduleDto>>(
                    $"{BaseUrl}/Schedule/Registed?userId={CurrentUserId}");

                var registeredScheduleIds = registered?.Select(r => r.ScheduleId).ToList() ?? new List<int>();

                if (scheduleList != null)
                {
                    RegisteredSchedules = scheduleList
                        .Where(s => registeredScheduleIds.Contains(s.ScheduleId))
                        .ToList();

                    Schedules = scheduleList
                        .Where(s => !registeredScheduleIds.Contains(s.ScheduleId))
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading data");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostRegisterAsync(int ScheduleId, string UserId)
        {
            BaseUrl = _apiSettings.BaseUrl;
            var registerData = new
            {
                userId = UserId,
                scheduleId = ScheduleId
            };

            var response = await _httpClient.PostAsJsonAsync($"{BaseUrl}/Schedule/register", registerData);

            if (response.IsSuccessStatusCode)
            {
                TempData["SuccessMessage"] = "Registration successful!";
            }
            else
            {
                TempData["ErrorMessage"] = "Registration failed. Please try again.";
            }

            return RedirectToPage(new { id = Id });
        }
    }
}
