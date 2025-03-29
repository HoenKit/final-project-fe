using final_project_fe.Dtos;
using final_project_fe.Dtos.Comment;
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

        public async Task OnGetAsync()
        {
            string apiUrl = $"{_apiSettings.BaseUrl}/Comment"; // URL của API 

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    Comments = JsonSerializer.Deserialize<PageResult<CommentDto>>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<CommentDto>(new List<CommentDto>(), 0, 1, 10);
                }
                else
                {
                    _logger.LogError($"Lỗi API: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
            }
        }
    }
}
