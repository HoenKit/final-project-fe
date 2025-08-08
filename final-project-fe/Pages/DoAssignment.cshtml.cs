using final_project_fe.Dtos.Assignment;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using static final_project_fe.Dtos.Lesson.QuizDto;

namespace final_project_fe.Pages
{
    public class DoAssignmentModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DoAssignmentModel> _logger;
        private readonly ApiSettings _apiSettings;

        public DoAssignmentModel(HttpClient httpClient, ILogger<DoAssignmentModel> logger, IOptions<ApiSettings> apiSettings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiSettings = apiSettings.Value;
        }

        public int? assignmentid { get; set; }
        public string? MeetingLink { get; set; }
        public UserAssignmentDto? Assignment { get; set; }
        public UserDto? UserDetail { get; set; }
        public string UserId { get; set; }
        public bool IsPresented { get; set; } = true;
        public bool IsUserAssignmentCreated { get; set; } = false;
        public string BaseUrl { get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            assignmentid = id;
            BaseUrl = _apiSettings.BaseUrl;

            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            // 🔓 Decode token
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
            UserId = jwtToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(UserId))
                return RedirectToPage("/Login");

            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var checkUrl = $"{BaseUrl}/Learning/user?assignmentId={id}&userId={UserId}";
            var shouldCreateAssignment = true;

            try
            {
                var response = await _httpClient.GetAsync(checkUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var existing = JsonSerializer.Deserialize<UserAssignmentDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (existing != null)
                    {
                        Assignment = existing;
                        IsPresented = existing.IsPresented;
                        if (!existing.IsScored)
                        {
                            IsUserAssignmentCreated = true;
                            shouldCreateAssignment = false;
                        }
                    }
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi kiểm tra UserAssignment.");
            }
            try
            {
                var assignmentResponse = await _httpClient.GetAsync($"{BaseUrl}/Assignment/{id}");
                if (assignmentResponse.IsSuccessStatusCode)
                {
                    var json = await assignmentResponse.Content.ReadAsStringAsync();
                    var assignment = JsonSerializer.Deserialize<AssignmentDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    MeetingLink = assignment?.meetLink;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin Assignment.");
            }

            if (shouldCreateAssignment)
            {
                try
                {
                    var postUrl = $"{BaseUrl}/Learning/DoAssignment";
                    var payload = new { UserId, assignmentId = id };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    var result = await _httpClient.PostAsync(postUrl, content);
                    IsUserAssignmentCreated = result.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Lỗi khi tạo UserAssignment.");
                }
            }

            // Load user info
            try
            {
                var userResponse = await _httpClient.GetAsync($"{BaseUrl}/User/GetUserById/{UserId}");
                if (userResponse.IsSuccessStatusCode)
                {
                    var json = await userResponse.Content.ReadAsStringAsync();
                    UserDetail = JsonSerializer.Deserialize<UserDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi lấy thông tin người dùng.");
            }

            return Page();
        }
        public IActionResult OnGetErrorSubmit()
        {
            TempData["ErrorMessage"] = "Nộp bài thất bại.";
            return RedirectToPage("/DoAssignment", new { id = assignmentid });
        }
    }
}
