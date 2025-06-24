using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Xml.Linq;
using final_project_fe.Dtos.Users;

namespace final_project_fe.Pages.Admin.UserManager
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        public IndexModel(ILogger<IndexModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public PageResult<User> Users { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; } = 6;

        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
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

            //Lấy trang trước đấy
            const string sessionKey = "PageHistory";
            var history = HttpContext.Session.GetString(sessionKey);
            List<string> pageHistory;

            if (string.IsNullOrEmpty(history))
            {
                pageHistory = new List<string>();
            }
            else
            {
                pageHistory = JsonSerializer.Deserialize<List<string>>(history);
            }

            // Lấy URL hiện tại
            var currentUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;

            // Chỉ thêm nếu khác trang cuối cùng
            if (pageHistory.Count == 0 || pageHistory.Last() != currentUrl)
            {
                pageHistory.Add(currentUrl);
            }

            // Lưu lại vào session
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(pageHistory));

            CurrentPage = pageNumber;

            string usersApiUrl = $"{_apiSettings.BaseUrl}/User?page={pageNumber}&pageSize={PageSize}";

            try
            {
                HttpResponseMessage usersResponse = await _httpClient.GetAsync(usersApiUrl);
                if (usersResponse.IsSuccessStatusCode)
                {
                    string usersJsonResponse = await usersResponse.Content.ReadAsStringAsync();
                    Users = JsonSerializer.Deserialize<PageResult<User>>(usersJsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<User>(new List<User>(), 0, 1, PageSize);
                }
                else
                {
                    _logger.LogError($"Lỗi API User: {usersResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }

            return Page();
        }

    }
}
