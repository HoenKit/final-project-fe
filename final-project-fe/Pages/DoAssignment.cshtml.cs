using final_project_fe.Dtos.Assignment;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
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
        public bool IsPresented { get; set; } = false;
        public bool IsUserAssignmentCreated { get; set; } = false;
        public bool IsAlreadyDone { get; set; } = false;
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

            var client = _httpClient;

            var checkUrl = $"{BaseUrl}/Learning/user?assignmentId={id}&userId={UserId}";
            var shouldCreateAssignment = true;

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, checkUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var existing = JsonSerializer.Deserialize<UserAssignmentDto>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (existing != null)
                    {
                        Assignment = existing;
                        IsPresented = existing.IsPresented;

                        if (!string.IsNullOrWhiteSpace(existing.Content))
                        {
                            IsAlreadyDone = true;
                            shouldCreateAssignment = false; // Không tạo mới nữa
                        }
                        else if (!existing.IsScored)
                        {
                            IsUserAssignmentCreated = true;
                            shouldCreateAssignment = false; // Không tạo mới nữa
                        }
                    }
                    else
                    {
                        IsUserAssignmentCreated = false;
                        IsPresented = false;   // đánh dấu chưa điểm danh
                        shouldCreateAssignment = false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while checking UserAssignment.");
            }

            try
            {
                var requestAssignment = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/Assignment/{id}");
                requestAssignment.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var assignmentResponse = await client.SendAsync(requestAssignment);
                if (assignmentResponse.IsSuccessStatusCode)
                {
                    var json = await assignmentResponse.Content.ReadAsStringAsync();
                    var assignment = JsonSerializer.Deserialize<AssignmentDto>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    MeetingLink = assignment?.meetLink;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving Exercise information.");
            }

            if (shouldCreateAssignment)
            {
                try
                {
                    var postUrl = $"{BaseUrl}/Learning/DoAssignment";
                    var payload = new { UserId, assignmentId = id };
                    var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                    var requestPost = new HttpRequestMessage(HttpMethod.Post, postUrl)
                    {
                        Content = content
                    };
                    requestPost.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                    var result = await client.SendAsync(requestPost);
                    IsUserAssignmentCreated = result.IsSuccessStatusCode;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating UserAssignment.");
                }
            }

            // Load user info
            try
            {
                var requestUser = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/User/GetUserById/{UserId}");
                requestUser.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var userResponse = await client.SendAsync(requestUser);
                if (userResponse.IsSuccessStatusCode)
                {
                    var json = await userResponse.Content.ReadAsStringAsync();
                    UserDetail = JsonSerializer.Deserialize<UserDto>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while getting user information.");
            }

            return Page();
        }

        public IActionResult OnGetErrorSubmit()
        {
            TempData["ErrorMessage"] = "Submission failed.";
            return RedirectToPage("/DoAssignment", new { id = assignmentid });
        }
    }
}
