using final_project_fe.Dtos.Post;
using final_project_fe.Dtos.Users;
using final_project_fe.Pages.Admin.UserManager;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;

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

        public PostManagerDto? Post { get; set; }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            string? token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

            string? role = JwtHelper.GetRoleFromToken(token);
            if (role != "Admin") return RedirectToPage("/Index");

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string apiUrl = $"{_apiSettings.BaseUrl}/Post/toggle-deleted/{id}";

                HttpResponseMessage response = await _httpClient.PutAsync(apiUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage("./Index");
                }
                else
                {
                    _logger.LogError($"Xóa post thất bại: {response.StatusCode}");
                    return StatusCode((int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi API khi xóa post: {ex.Message}");
                return StatusCode(500);
            }
        }
    }
}
