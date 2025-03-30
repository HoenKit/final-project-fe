using final_project_fe.Dtos;
using final_project_fe.Dtos.Comment;
using final_project_fe.Dtos.Post;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Xml.Linq;

namespace final_project_fe.Pages
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
        public PageResult<CommentDto> Comments { get; set; }
        public PageResult<PostDto> Posts { get; set; }
        public async Task OnGetAsync()
        {
            // URL Post API
            string postsApiUrl = $"{_apiSettings.BaseUrl}/Post";

            // URL Comment API
            string commentsApiUrl = $"{_apiSettings.BaseUrl}/Comment";

            try
            {
                // Gọi API Posts
                HttpResponseMessage postsResponse = await _httpClient.GetAsync(postsApiUrl);
                if (postsResponse.IsSuccessStatusCode)
                {
                    string postsJsonResponse = await postsResponse.Content.ReadAsStringAsync();
                    Posts = JsonSerializer.Deserialize<PageResult<PostDto>>(postsJsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<PostDto>(new List<PostDto>(), 0, 1, 10);
                }
                else
                {
                    _logger.LogError($"Lỗi API Post: {postsResponse.StatusCode}");
                }

                // Gọi API Comments
                HttpResponseMessage commentsResponse = await _httpClient.GetAsync(commentsApiUrl);
                if (commentsResponse.IsSuccessStatusCode)
                {
                    string commentsJsonResponse = await commentsResponse.Content.ReadAsStringAsync();
                    Comments = JsonSerializer.Deserialize<PageResult<CommentDto>>(commentsJsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<CommentDto>(new List<CommentDto>(), 0, 1, 10);
                }
                else
                {
                    _logger.LogError($"Lỗi API Comment: {commentsResponse.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }
        }
    }
}
