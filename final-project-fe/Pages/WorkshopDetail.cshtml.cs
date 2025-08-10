using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.WorkShop;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;

namespace final_project_fe.Pages
{
    public class WorkshopDetailModel : PageModel
    {
            private readonly ILogger<WorkshopDetailModel> _logger;
            private readonly ApiSettings _apiSettings;
            private readonly HttpClient _httpClient;
            private readonly IHttpClientFactory _httpClientFactory;


            public WorkshopDetailModel(ILogger<WorkshopDetailModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient, IHttpClientFactory httpClientFactory)
            {
                _logger = logger;
                _apiSettings = apiSettings.Value;
                _httpClient = httpClient;
                _httpClientFactory = httpClientFactory;
            }
            public string BaseUrl { get; set; }
            public GetMentorDto? Mentor { get; set; }
            public bool IsOwner { get; set; }
            [BindProperty]
            public WorkShopDto? Workshop { get; set; }
            public GetMentorDto? CreatorMentor { get; set; }
            public int? CurrentMentorId;
            public async Task<IActionResult> OnGetAsync(int id)
            {
                BaseUrl = _apiSettings.BaseUrl;

                var token = Request.Cookies["AccessToken"];
                string? userId = null;

                if (!string.IsNullOrEmpty(token))
                {
                    var handler = new JwtSecurityTokenHandler();
                    var jwtToken = handler.ReadJwtToken(token);
                    var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

                    if (userIdClaim != null)
                    {
                        userId = userIdClaim.Value;
                    }
                }

                var client = _httpClientFactory.CreateClient();

                if (!string.IsNullOrEmpty(token))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

            // Step 1: Get current Mentor if userId is available
            if (!string.IsNullOrEmpty(userId))
            {
                var mentorResponse = await client.GetAsync($"{BaseUrl}/Mentor/get-by-user/{userId}");
                if (mentorResponse.IsSuccessStatusCode)
                {
                    var mentorJson = await mentorResponse.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(mentorJson) && mentorJson != "null")
                    {
                        Mentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                }
            }
            CurrentMentorId = Mentor?.MentorId;
            // Step 2: Get Workshop by Id (this is always required)
            var workshopResponse = await client.GetAsync($"{BaseUrl}/WorkShop/{id}");
                if (!workshopResponse.IsSuccessStatusCode)
                {
                    ModelState.AddModelError(string.Empty, "Failed to retrieve workshop info.");
                    return Page();
                }

                var workshopJson = await workshopResponse.Content.ReadAsStringAsync();
                Workshop = JsonSerializer.Deserialize<WorkShopDto>(workshopJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (Mentor?.MentorId == Workshop?.MentorId)
                {
                    IsOwner = true;
                }
            // Step 3: Get Creator Mentor by Workshop.MentorId
            if (Workshop?.MentorId != null)
                {
                    var creatorMentorResponse = await client.GetAsync($"{BaseUrl}/Mentor/{Workshop.MentorId}");
                    if (creatorMentorResponse.IsSuccessStatusCode)
                    {
                        var creatorJson = await creatorMentorResponse.Content.ReadAsStringAsync();
                        CreatorMentor = JsonSerializer.Deserialize<GetMentorDto>(creatorJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    }
                }
            return Page();
            }
            public async Task<IActionResult> OnPostEditAsync()
            {
                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    TempData["ErrorMessage"] = "Token is missing.";
                    return RedirectToPage("/WorkshopDetail", new { workshopId = Workshop?.WorkShopId });
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var json = JsonSerializer.Serialize(Workshop);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await client.PutAsync($"{_apiSettings.BaseUrl}/WorkShop", content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Workshop updated successfully.";
                }
                else
                {
                    TempData["ErrorMessage"] = "Workshop update failed.";
                }

                return RedirectToPage("/WorkshopDetail", new { workshopId = Workshop?.WorkShopId });
            }

            public async Task<IActionResult> OnPostDeleteAsync(int WorkshopId)
            {
                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    TempData["ErrorMessage"] = "Token is missing.";
                    return RedirectToPage("/WorkshopDetail", new { workshopId = WorkshopId });
                }

                var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await client.DeleteAsync($"{_apiSettings.BaseUrl}/WorkShop/{WorkshopId}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Workshop has been deleted.";
                    return RedirectToPage("/Index");
                }
                else
                {
                    TempData["ErrorMessage"] = "Delete workshop failed.";
                    return RedirectToPage("/WorkshopDetail", new { workshopId = WorkshopId });
                }
            }
        }
    }
