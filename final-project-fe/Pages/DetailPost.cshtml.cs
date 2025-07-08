using final_project_fe.Dtos.Comment;
using final_project_fe.Dtos.Post;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace final_project_fe.Pages
{
    public class DetailPostModel : PageModel
    {
        private readonly ILogger<DetailPostModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public DetailPostModel(ILogger<DetailPostModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public PostDetail Post { get; set; }
        public List<PostFileDto> PostFile { get; set; }
        public List<CommentPostDetailDto> Comment { get; set; }
        public string Role {  get; set; }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (!Request.Cookies.ContainsKey("AccessToken"))
                return RedirectToPage("/Login");
            string token = Request.Cookies["AccessToken"];
            Role = JwtHelper.GetRoleFromToken(token);

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

            string postUrl = $"{_apiSettings.BaseUrl}/Post/GetDetail/{id}";
            var postRequest = new HttpRequestMessage(HttpMethod.Get, postUrl);
            postRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var postResponse = await _httpClient.SendAsync(postRequest);
            if (!postResponse.IsSuccessStatusCode)
            {
                return NotFound();
            }

            var postJson = await postResponse.Content.ReadAsStringAsync();
            var postRoot = JsonNode.Parse(postJson);
            Post = postRoot.Deserialize<PostDetail>(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new PostDetail();

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

            return RedirectToPage("/Admin/Dashboard/Index");
        }
    }
}
