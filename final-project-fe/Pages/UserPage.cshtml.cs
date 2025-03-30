using final_project_fe.Dtos.Post;
using System.Text.Json;
using System.Text;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using final_project_fe.Dtos;

namespace final_project_fe.Pages
{
    public class UserPageModel : PageModel
    {
        private readonly ILogger<UserPageModel> _logger;
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;

        public UserPageModel(ILogger<UserPageModel> logger, HttpClient httpClient, IOptions<ApiSettings> apiSettings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _apiSettings = apiSettings.Value;
        }

        public PageResult<PostDto> Posts { get; set; }

        [BindProperty]
        public string PostContent { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostCreatePostAsync()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var userId = Guid.TryParse("A91346D0-1690-401E-812B-1F75B222D598", out var parsedGuid) ? parsedGuid : Guid.Empty;

            var postDto = new PostDto
            {
                Content = PostContent,
                Title = "Post Title",
                UserId = userId,
                SubCategoryId = 1,
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(postDto), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/Post", jsonContent);

            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage("/UserPage");
            }
            else
            {
                // Xử lý lỗi nếu có
                ModelState.AddModelError(string.Empty, "Không thể đăng bài, vui lòng thử lại.");
                return Page();
            }
        }
    }
}