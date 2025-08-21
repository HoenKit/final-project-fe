using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace final_project_fe.Pages
{

    public class EventPageModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;

        public EventPageModel(HttpClient httpClient, IOptions<ApiSettings> apiSettings)
        {
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public string BaseUrl { get; set; }

        [BindProperty]
        public string UserId { get; set; }

        [BindProperty]
        public string Email { get; set; }
        public int CurrentTurns { get; set; }
        public decimal? TotalPoints { get; set; }
        public string Message { get; set; }
        public bool IsSuccess { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            await LoadUserData();
            return Page();
        }

        // 🔹 Thêm Authorization Header từ cookie token
        private void AddAuthorizationHeader()
        {
            string token = Request.Cookies["AccessToken"];
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private async Task LoadUserData()
        {
            BaseUrl = _apiSettings.BaseUrl;

            string token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                RedirectToPage("/Login");
                return;
            }

            AddAuthorizationHeader(); // ✅ Gắn token trước khi gọi API

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            UserId = jsonToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            var apiUrl = $"{BaseUrl}/User/GetUserById/{UserId}";
            var response = await _httpClient.GetAsync(apiUrl);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var user = JsonSerializer.Deserialize<User>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (user != null && user.IsPremium == false)
                {
                    TempData["ErrorMessage"] = "This is for Premium User only";
                    Response.Redirect("/Index");
                    return;
                }

                if (user != null)
                {
                    CurrentTurns = user.Turns;
                    TotalPoints = user.Point;
                    Email = user.Email;
                }
            }
        }

        public async Task<IActionResult> OnPostClaimTurnAsync()
        {
            BaseUrl = _apiSettings.BaseUrl;
            try
            {
                AddAuthorizationHeader(); // ✅ Gắn token

                var apiUrl = $"{BaseUrl}/Event/add-turns?userId={UserId}";
                var response = await _httpClient.PostAsync(apiUrl, null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "You have received 1 free spin!";
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    if (responseContent.Contains("Already logged in today", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["InfoMessage"] = "You are logged in today";
                    }
                    else
                    {
                        Message = "Unable to accept spin. Please try again later.";
                    }
                    IsSuccess = false;
                }
                else
                {
                    Message = "Unable to accept spin. Please try again later.";
                    IsSuccess = false;
                }

                TempData["ErrorMessage"] = Message;

                await LoadUserData();
            }
            catch (Exception ex)
            {
                Message = $"Error: {ex.Message}";
                IsSuccess = false;
                TempData["ErrorMessage"] = Message;
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSpinAsync()
        {
            BaseUrl = _apiSettings.BaseUrl;
            try
            {
                AddAuthorizationHeader(); // ✅ Gắn token

                // Create pool
                var weightedPoints = new List<int>();
                for (int i = 1; i <= 10; i++)
                    weightedPoints.AddRange(Enumerable.Repeat(i, 50));
                for (int i = 11; i <= 20; i++)
                    weightedPoints.AddRange(Enumerable.Repeat(i, 30));
                for (int i = 21; i <= 39; i++)
                    weightedPoints.AddRange(Enumerable.Repeat(i, 10));
                for (int i = 40; i <= 50; i++)
                    weightedPoints.Add(i);

                Random random = new Random();
                int pointsWon = weightedPoints[random.Next(weightedPoints.Count)];

                var apiUrl = $"{BaseUrl}/Event/add-points";
                var requestData = new
                {
                    userId = UserId,
                    points = pointsWon
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync(apiUrl, content);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = $"🎉 Congratulations! You received {pointsWon} points!";
                }
                else
                {
                    var errorMessage = await response.Content.ReadAsStringAsync();

                    if (errorMessage.Contains("No turns left", StringComparison.OrdinalIgnoreCase))
                    {
                        TempData["ErrorMessage"] = "You have no Turn left!";
                    }
                    else
                    {
                        TempData["ErrorMessage"] = "Unable to spin. Please try again.";
                    }
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error: {ex.Message}";
            }

            return RedirectToPage("/EventPage");
        }
    }
}