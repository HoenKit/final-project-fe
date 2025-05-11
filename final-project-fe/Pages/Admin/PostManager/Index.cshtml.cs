using final_project_fe.Dtos;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Post;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace final_project_fe.Pages.Admin.PostManager
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

        public PageResult<PostManagerDto> Posts { get; set; }
        public int CurrentPage { get; set; }
        public int PageSize { get; set; } = 10;

        public async Task<IActionResult> OnGetAsync(int pageNumber = 1)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            if (role != "Admin")
                return RedirectToPage("/Index");

            CurrentPage = pageNumber;

            string apiUrl = $"{_apiSettings.BaseUrl}/Post/GetAllPosts?page={pageNumber}&pageSize={PageSize}";

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync();
                    Posts = JsonSerializer.Deserialize<PageResult<PostManagerDto>>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? Posts;
                }
                else
                {
                    _logger.LogError($"Lỗi khi gọi API Post: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API Post: {ex.Message}");
            }

            return Page();
        }
    }
}
