using final_project_fe.Dtos;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using final_project_fe.Dtos.UserManager;
using Microsoft.Extensions.Hosting;
using System.Text.Json;
using System.Xml.Linq;

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

		public PageResult<UserManagerDto> Users { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; } = 10;

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

            CurrentPage = pageNumber;

            // URL API có phân trang
            string usersApiUrl = $"{_apiSettings.BaseUrl}/UserManager?page={pageNumber}&pageSize={PageSize}";

            try
            {
                HttpResponseMessage usersResponse = await _httpClient.GetAsync(usersApiUrl);
                if (usersResponse.IsSuccessStatusCode)
                {
                    string usersJsonResponse = await usersResponse.Content.ReadAsStringAsync();
                    Users = JsonSerializer.Deserialize<PageResult<UserManagerDto>>(usersJsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<UserManagerDto>(new List<UserManagerDto>(), 0, 1, PageSize);
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
