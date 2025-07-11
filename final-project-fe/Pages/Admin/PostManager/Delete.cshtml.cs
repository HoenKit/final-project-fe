using final_project_fe.Dtos.Notification;
using final_project_fe.Dtos.Post;
using final_project_fe.Dtos.Users;
using final_project_fe.Pages.Admin.UserManager;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace final_project_fe.Pages.Admin.PostManager
{
    public class DeleteModel : PageModel
    {
        private readonly ILogger<DeleteModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        public DeleteModel(ILogger<DeleteModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public PostDetail? Post { get; set; }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            string? token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

            string? role = JwtHelper.GetRoleFromToken(token);
            if (role != "Admin") return RedirectToPage("/Index");

            try
            {
                string postUrl = $"{_apiSettings.BaseUrl}/Post/GetDetail/{id}";
                var postRequest = new HttpRequestMessage(HttpMethod.Get, postUrl);
                postRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var postResponse = await _httpClient.SendAsync(postRequest);
                if (!postResponse.IsSuccessStatusCode)
                {
                    return RedirectToPage("/Page/ErrorPage");
                }

                var postJson = await postResponse.Content.ReadAsStringAsync();
                var postRoot = JsonNode.Parse(postJson);
                Post = postRoot.Deserialize<PostDetail>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new PostDetail();

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string apiUrl = $"{_apiSettings.BaseUrl}/Post/toggle-deleted/{id}";

                HttpResponseMessage response = await _httpClient.PutAsync(apiUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    if (Post.IsDeleted != true)
                    {
                        var notification = new CreateNotification
                        {
                            userId = Post.UserId,
                            message = $"Your article titled '{Post.Title}' has been removed for violating our standards."
                        };

                        var content = new StringContent(JsonSerializer.Serialize(notification), System.Text.Encoding.UTF8, "application/json");

                        string notiApiUrl = $"{_apiSettings.BaseUrl}/Notification";
                        var notiResponse = await _httpClient.PostAsync(notiApiUrl, content);

                        if (!notiResponse.IsSuccessStatusCode)
                        {
                            _logger.LogError($"Gửi thông báo thất bại: {notiResponse.StatusCode}");
                        }

                        TempData["SuccessMessage"] = $"The article has been successfully deleted.";
                    }
                    else
                    {
                        var notification = new CreateNotification
                        {
                            userId = Post.UserId,
                            message = $"Your article titled '{Post.Title}' has been restored after we reviewed the article again."
                        };

                        var content = new StringContent(JsonSerializer.Serialize(notification), System.Text.Encoding.UTF8, "application/json");

                        string notiApiUrl = $"{_apiSettings.BaseUrl}/Notification";
                        var notiResponse = await _httpClient.PostAsync(notiApiUrl, content);

                        if (!notiResponse.IsSuccessStatusCode)
                        {
                            _logger.LogError($"Gửi thông báo thất bại: {notiResponse.StatusCode}");
                        }
                        TempData["SuccessMessage"] = $"The article has been successfully restored.";
                    }

                    return RedirectToPage("./Index");
                }
                else
                {
                    _logger.LogError($"Xóa post thất bại: {response.StatusCode}");
                    TempData["ErrorMessage"] = $"Article deleted failed.";
                    return RedirectToPage("./Index");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi API khi xóa post: {ex.Message}");
                return RedirectToPage("/Page/ErrorPage");
            }
        }
    }
}
