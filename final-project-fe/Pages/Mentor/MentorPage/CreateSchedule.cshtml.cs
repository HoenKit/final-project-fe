using final_project_fe.Dtos;
using final_project_fe.Dtos.Assignment;
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

            string userId;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);
                userId = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub")?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    ModelState.AddModelError(string.Empty, "User ID not found in token.");
                    return;
                }
            }
            catch
            {
                ModelState.AddModelError(string.Empty, "Invalid token.");
                return;
            }

            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                // Step 1: Get mentor info
                var mentorResponse = await client.GetAsync($"{BaseUrl}/Mentor/get-by-user/{userId}");
                if (!mentorResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(string.Empty, "Failed to retrieve mentor info.");
                    return;
                }

                var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                Mentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (Mentor == null)
                {
                    ModelState.AddModelError(string.Empty, "Mentor data is null.");
                    return;
                }

                // Step 2: Get approved courses for this mentor
                var courseResponse = await client.GetAsync($"{BaseUrl}/Course?mentorId={Mentor.MentorId}&statuses=Approved");
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mentor or courses.");
                ModelState.AddModelError(string.Empty, "An unexpected error occurred while loading data.");
            }
        }
    }
}
