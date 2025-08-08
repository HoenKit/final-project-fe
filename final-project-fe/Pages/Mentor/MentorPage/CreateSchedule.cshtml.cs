using final_project_fe.Dtos;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Pages.Mentor.MentorPage;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace final_project_fe.Pages.Mentor
{
    public class CreateScheduleModel : PageModel
    {
        private readonly ILogger<CreateScheduleModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public CreateScheduleModel(ILogger<CreateScheduleModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient, IHttpClientFactory httpClientFactory)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
            _httpClientFactory = httpClientFactory;
        }

        public string BaseUrl { get; set; }
        public GetMentorDto Mentor { get; set; }
        public List<ListCourseDto> Course { get; set; }
        public async Task OnGet()
        {
            BaseUrl = _apiSettings.BaseUrl;

            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                ModelState.AddModelError(string.Empty, "Token is missing.");
                return;
            }

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");
            if (userIdClaim == null)
            {
                ModelState.AddModelError(string.Empty, "User ID not found in token.");
                return;
            }

            string userId = userIdClaim.Value;

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Step 1: Get mentor info
            var mentorResponse = await client.GetAsync($"{BaseUrl}/Mentor/get-by-user/{userId}");
            if (!mentorResponse.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "Failed to retrieve mentor info.");
                return;
            }

            var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
            var mentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (mentor == null)
            {
                ModelState.AddModelError(string.Empty, "Mentor data is null.");
                return;
            }

            Mentor = mentor;

            // Step 2: Get courses for this mentor
            var courseResponse = await client.GetAsync($"{BaseUrl}/Course?mentorId={mentor.MentorId}&statuses=Approved");
            if (!courseResponse.IsSuccessStatusCode)
            {
                ModelState.AddModelError(string.Empty, "Failed to retrieve courses.");
                return;
            }

            var courseJson = await courseResponse.Content.ReadAsStringAsync();

            var result = JsonSerializer.Deserialize<PageResult<ListCourseDto>>(courseJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            Course = result?.Items?.ToList() ?? new List<ListCourseDto>();
        }
    }
}
