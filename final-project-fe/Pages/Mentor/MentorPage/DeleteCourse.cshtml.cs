using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Post;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class DeleteCourseModel : PageModel
    {
        private readonly ILogger<DeleteCourseModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        public DeleteCourseModel(ILogger<DeleteCourseModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public GetMentorDto? Course { get; set; }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            string? token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

            string? role = JwtHelper.GetRoleFromToken(token);
            if (role != "Admin" || role != "Mentor") return RedirectToPage("/Index");

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string apiUrl = $"{_apiSettings.BaseUrl}/Course/toggle-deleted/{id}";

                HttpResponseMessage response = await _httpClient.PutAsync(apiUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage("./Index");
                }
                else
                {
                    _logger.LogError($"Xóa course thất bại: {response.StatusCode}");
                    return StatusCode((int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi API khi xóa course: {ex.Message}");
                return StatusCode(500);
            }
        }
    }
}
