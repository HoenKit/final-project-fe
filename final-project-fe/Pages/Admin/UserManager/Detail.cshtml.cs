using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace final_project_fe.Pages.Admin.UserManager
{
    public class DetailModel : PageModel
    {
        private readonly ILogger<DetailModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public DetailModel(ILogger<DetailModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public User UserDetail { get; set; } = new();
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";
        public string AvatarUrl { get; set; } = string.Empty;

        public async Task<IActionResult> OnGetAsync(Guid userId)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
            {
                return RedirectToPage("/Login");
            }

            string token = Request.Cookies["AccessToken"];

            string? role = JwtHelper.GetRoleFromToken(token);
            if (role != "Admin")
            {
                return RedirectToPage("/Index");
            }

            if (userId == Guid.Empty)
            {
                return BadRequest("Invalid user ID.");
            }

            // Lấy lịch sử trang
            const string sessionKey = "PageHistory";
            var history = HttpContext.Session.GetString(sessionKey);
            List<string> pageHistory = string.IsNullOrEmpty(history)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(history);

            var currentUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;

            if (pageHistory.Count == 0 || pageHistory.Last() != currentUrl)
            {
                pageHistory.Add(currentUrl);
            }

            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(pageHistory));

            string apiUrl = $"{_apiSettings.BaseUrl}/User/GetUserById/{userId}";

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    UserDetail = JsonSerializer.Deserialize<User>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new User();

                    // Nếu UserMetaData null -> tạo mặc định
                    if (UserDetail.UserMetaData == null)
                    {
                        UserDetail.UserMetaData = new UserMetadata
                        {
                            FirstName = "N/A",
                            LastName = "N/A",
                            Gender = "Unknown",
                            Avatar = "https://i.pinimg.com/736x/bc/43/98/bc439871417621836a0eeea768d60944.jpg",
                            Address = "None"
                        };
                    }

                    // Gọi ImageUrlHelper để lấy URL Avatar
                    var avatarPath = string.IsNullOrEmpty(UserDetail.UserMetaData.Avatar)
                        ? "default-avatar.jpg"
                        : UserDetail.UserMetaData.Avatar;

                    AvatarUrl = ImageUrlHelper.AppendSasTokenIfNeeded(avatarPath, SasToken);
                }
                else
                {
                    _logger.LogError($"Failed to fetch user details: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error calling API: {ex.Message}");
            }

            return Page();
        }

        public IActionResult OnPostBack()
        {
            const string sessionKey = "PageHistory";
            var history = HttpContext.Session.GetString(sessionKey);
            if (!string.IsNullOrEmpty(history))
            {
                var pageHistory = JsonSerializer.Deserialize<List<string>>(history);

                if (pageHistory.Count > 1)
                {
                    pageHistory.RemoveAt(pageHistory.Count - 1);
                    var previousPage = pageHistory.Last();

                    HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(pageHistory));

                    return Redirect(previousPage);
                }
            }

            return RedirectToPage("/Admin/Dashboard/Index");
        }
    }
}
