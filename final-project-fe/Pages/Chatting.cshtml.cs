using final_project_fe.Dtos.NewFolder;
using final_project_fe.Dtos.Users;
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
    public class ChattingModel : PageModel
    {
        private readonly ILogger<ChattingModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private readonly SignalrSetting _signalrSetting;

        public ChattingModel(ILogger<ChattingModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient, SignalrSetting signalrSetting)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
            _signalrSetting = signalrSetting;
        }
        public string HubUrl { get; set; }
        public string BaseUrl { get; set; }
        public string CurrentUserId { get; set; }
        public string  PartnerId { get; set; }
        public List<MessageDto> Messages { get; set; } = new List<MessageDto>();

        public async Task<IActionResult> OnGetAsync(string partnerId)
        {
            HubUrl = _signalrSetting.HubUrl;
            BaseUrl = _apiSettings.BaseUrl;

            string token = Request.Cookies["AccessToken"];
            var handler = new JwtSecurityTokenHandler();

            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Token not found");
                TempData["ErrorMessage"] = "You are not logged in yet.";
                return RedirectToPage("/Login");
            }

            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            if (jsonToken == null)
            {
                _logger.LogError("Invalid token");
                TempData["ErrorMessage"] = "Invalid token.";
                return RedirectToPage("/Login");
            }

            CurrentUserId = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            var roles = jsonToken.Claims
                .Where(c => c.Type == ClaimTypes.Role)
                .Select(c => c.Value)
                .ToList();

            if (string.IsNullOrEmpty(CurrentUserId) || roles == null || roles.Count == 0)
            {
                _logger.LogError("Token missing UserId or Role");
                TempData["ErrorMessage"] = "Unable to determine access rights.";
                return RedirectToPage("/ErrorPage");
            }

            using var client = new HttpClient();
            client.BaseAddress = new Uri(BaseUrl);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                var response = await client.GetAsync($"{BaseUrl}/User/GetUserById/{CurrentUserId}");
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    var user = JsonSerializer.Deserialize<UserDto>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (user == null || (!user.IsPremium && !roles.Contains("Mentor")))
                    {
                        TempData["ErrorMessage"] = "You do not have permission to use this feature.";
                        return RedirectToPage("/Index");
                    }
                }
                else
                {
                    _logger.LogError($"Lỗi khi gọi API GetUserById: {response.StatusCode}");
                    TempData["ErrorMessage"] = "Không thể kiểm tra quyền truy cập.";
                    return RedirectToPage("/ErrorPage");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while calling GetUserById");
                TempData["ErrorMessage"] = "System error while checking permissions.";
                return RedirectToPage("/ErrorPage");
            }

            // ⭐ FIX: Chỉ load messages nếu có partnerId
            if (!string.IsNullOrEmpty(partnerId))
            {
                using var client2 = new HttpClient();
                client2.BaseAddress = new Uri(BaseUrl);
                client2.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                try
                {
                    // ⭐ Log để debug
                    _logger.LogInformation($"Loading messages between {CurrentUserId} and {partnerId}");

                    var responseMessages = await client2.GetAsync($"{BaseUrl}/Messages/Room?userId1={CurrentUserId}&userId2={partnerId}");

                    if (responseMessages.IsSuccessStatusCode)
                    {
                        var json = await responseMessages.Content.ReadAsStringAsync();
                        Messages = JsonSerializer.Deserialize<List<MessageDto>>(json, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        _logger.LogInformation($"Loaded {Messages?.Count ?? 0} messages");
                    }
                    else
                    {
                        _logger.LogWarning($"Failed to load messages: {responseMessages.StatusCode}");
                        Messages = new List<MessageDto>(); // Chưa có tin nhắn thì để rỗng
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error loading messages between {CurrentUserId} and {partnerId}");
                    Messages = new List<MessageDto>();
                }
            }
            else
            {
                // ⭐ Không có partnerId thì Messages để empty
                Messages = new List<MessageDto>();
            }

            PartnerId = partnerId; // ⭐ Bỏ .ToString() vì partnerId đã là string

            return Page();
        }
    }
    }


