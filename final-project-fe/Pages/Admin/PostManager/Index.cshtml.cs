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
        public PageResult<SubCategoryDto> SubCategories { get; set; }
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

            string postApiUrl = $"{_apiSettings.BaseUrl}/Post?page={pageNumber}&pageSize={PageSize}";
            string userApiUrl = $"{_apiSettings.BaseUrl}/User/";
            string subCategoryApiUrl = $"{_apiSettings.BaseUrl}/SubCategory/";

            try
            {
                var requestPost = new HttpRequestMessage(HttpMethod.Get, postApiUrl);
                requestPost.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestUser = new HttpRequestMessage(HttpMethod.Get, userApiUrl);
                requestUser.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var requestSubCategory = new HttpRequestMessage(HttpMethod.Get, subCategoryApiUrl);
                requestSubCategory.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi api song song 3 request
                var postTask = _httpClient.SendAsync(requestPost);
                var userTask = _httpClient.SendAsync(requestUser);
                var subCategoryTask = _httpClient.SendAsync(requestSubCategory);

                await Task.WhenAll(postTask, userTask, subCategoryTask);

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

                if (subCategoryTask.Result.IsSuccessStatusCode)
                {
                    string json = await subCategoryTask.Result.Content.ReadAsStringAsync();
                    SubCategories = JsonSerializer.Deserialize<PageResult<SubCategoryDto>>(json, options);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }

            Posts ??= new PageResult<PostManagerDto>(Enumerable.Empty<PostManagerDto>(), 0, CurrentPage, PageSize);
            Users ??= new PageResult<User>(Enumerable.Empty<User>(), 0, 1, 10);
            SubCategories ??= new PageResult<SubCategoryDto>(Enumerable.Empty<SubCategoryDto>(), 0, 1, 10);

            return Page();
        }
    }
}
