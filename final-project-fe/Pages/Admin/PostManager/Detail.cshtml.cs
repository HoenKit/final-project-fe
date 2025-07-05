using final_project_fe.Dtos;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Post;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace final_project_fe.Pages.Admin.PostManager
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

        public PostManagerDto? Post { get; set; }

        public User? User { get; set; }
        public CategoryDto? Category { get; set; }
        public List<PostFileDto> PostFiles { get; set; } = new();

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");

            string token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);

            if (role != "Admin")
                return RedirectToPage("/Index");

            //Lưu trang trước đấy
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

            // Lấy thông tin Post
            string postUrl = $"{_apiSettings.BaseUrl}/Post/{id}";
            var postRequest = new HttpRequestMessage(HttpMethod.Get, postUrl);
            postRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var postResponse = await _httpClient.SendAsync(postRequest);
            if (!postResponse.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var postJson = await postResponse.Content.ReadAsStringAsync();
            var postRoot = JsonNode.Parse(postJson);
            Post = postRoot.Deserialize<PostManagerDto>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new PostManagerDto();

            if (Post == null) return NotFound();

            // Lấy thông tin User
            string userUrl = $"{_apiSettings.BaseUrl}/User/GetUserById/{Post.UserId}";
            var userResponse = await _httpClient.GetAsync(userUrl);
            if (userResponse.IsSuccessStatusCode)
            {
                var userJson = await userResponse.Content.ReadAsStringAsync();
                User = JsonSerializer.Deserialize<User>(userJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }

            // Lấy thông tin Category
            string categoryUrl = $"{_apiSettings.BaseUrl}/Category/{Post.CategoryId}";
            var categoryResponse = await _httpClient.GetAsync(categoryUrl);
            if (categoryResponse.IsSuccessStatusCode)
            {
                var categoryJson = await categoryResponse.Content.ReadAsStringAsync();
                var categoryRoot = JsonNode.Parse(categoryJson);
                Category = categoryRoot?.Deserialize<CategoryDto>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new CategoryDto();
            }

            // Gọi API lấy PostFile
            string postFileUrl = $"{_apiSettings.BaseUrl}/PostFile?postId={id}";
            var postFileRequest = new HttpRequestMessage(HttpMethod.Get, postFileUrl);
            postFileRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var postFileResponse = await _httpClient.SendAsync(postFileRequest);
            if (postFileResponse.IsSuccessStatusCode)
            {
                var fileJson = await postFileResponse.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                PostFiles = JsonSerializer.Deserialize<List<PostFileDto>>(fileJson, options) ?? new();
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
                    // Xóa trang hiện tại
                    pageHistory.RemoveAt(pageHistory.Count - 1);
                    var previousPage = pageHistory.Last();

                    // Lưu lại session sau khi back
                    HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(pageHistory));

                    return Redirect(previousPage);
                }
            }

            // Nếu không có lịch sử, quay về trang chủ
            return RedirectToPage("/Admin/Dashboard/Index");
        }

    }
}