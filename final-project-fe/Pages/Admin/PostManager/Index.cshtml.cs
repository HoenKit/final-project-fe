using final_project_fe.Dtos;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Post;
using final_project_fe.Dtos.Users;
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
        public PageResult<User> Users { get; set; }
        public PageResult<CategoryDto> Categories { get; set; }
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

            string postApiUrl = $"{_apiSettings.BaseUrl}/Post?page={pageNumber}";
            string userApiUrl = $"{_apiSettings.BaseUrl}/User/";
            string categoryApiUrl = $"{_apiSettings.BaseUrl}/Category/";

            try
            {
                var requestPost = new HttpRequestMessage(HttpMethod.Get, postApiUrl);
                requestPost.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestUser = new HttpRequestMessage(HttpMethod.Get, userApiUrl);
                requestUser.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestCategory = new HttpRequestMessage(HttpMethod.Get, categoryApiUrl);
                requestCategory.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi api song song 3 request
                var postTask = _httpClient.SendAsync(requestPost);
                var userTask = _httpClient.SendAsync(requestUser);
                var categoryTask = _httpClient.SendAsync(requestCategory);

                await Task.WhenAll(postTask, userTask, categoryTask);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

                // Xử lý kết quả lấy được
                if (postTask.Result.IsSuccessStatusCode)
                {
                    string json = await postTask.Result.Content.ReadAsStringAsync();
                    Posts = JsonSerializer.Deserialize<PageResult<PostManagerDto>>(json, options);
                }

                if (userTask.Result.IsSuccessStatusCode)
                {
                    string json = await userTask.Result.Content.ReadAsStringAsync();
                    Users = JsonSerializer.Deserialize<PageResult<User>>(json, options);
                }

                if (categoryTask.Result.IsSuccessStatusCode)
                {
                    string json = await categoryTask.Result.Content.ReadAsStringAsync();
                    Categories = JsonSerializer.Deserialize<PageResult<CategoryDto>>(json, options);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }

            Posts ??= new PageResult<PostManagerDto>(Enumerable.Empty<PostManagerDto>(), 0, CurrentPage, PageSize);
            Users ??= new PageResult<User>(Enumerable.Empty<User>(), 0, 1, 10);
            Categories ??= new PageResult<CategoryDto>(Enumerable.Empty<CategoryDto>(), 0, 1, 10);

            return Page();
        }

    }
}
