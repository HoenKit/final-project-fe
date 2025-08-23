using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.WorkShop;
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

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class CreateWorkShopModel : PageModel
    {
        private readonly ILogger<CreateWorkShopModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private readonly IHttpClientFactory _httpClientFactory;
        public CreateWorkShopModel(ILogger<CreateWorkShopModel> logger, IOptions<ApiSettings> apiSettings, IHttpClientFactory httpClientFactory, HttpClient httpClient)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }
        public string BaseUrl { get; set; }
        public string CurrentUserId { get; set; }
        public int MentorId { get; set; }
        [BindProperty]
        public WorkshopRequest Workshop { get; set; } = new WorkshopRequest();

        public async Task OnGet()
        {
            BaseUrl = _apiSettings.BaseUrl;

            var token = Request.Cookies["accessToken"];
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

            // Tạo request GET với token riêng
            var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/Mentor/get-by-user/{userId}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var mentor = JsonSerializer.Deserialize<MentorResponse>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (mentor != null)
                {
                    MentorId = mentor.MentorId;
                }
            }
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            BaseUrl = _apiSettings.BaseUrl;

            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                ModelState.AddModelError(string.Empty, "Token missing.");
                return RedirectToPage("/Login");
            }


            var client = _httpClientFactory.CreateClient();

            // Tạo request POST với token riêng
            var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/workshop");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var json = JsonSerializer.Serialize(Workshop);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await client.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Workshop created successfully.";
                    return RedirectToPage("/Index");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    TempData["ErrorMessage"] = $"Creating a workshop failed: {error}";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling workshop API.");
                ModelState.AddModelError(string.Empty, "An error occurred. Please try again.");
                return Page();
            }
        }
    }
}
