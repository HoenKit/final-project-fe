using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Notification;
using final_project_fe.Dtos.Post;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

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

        public CourseResponseDto Course { get; set; }
        public CreateNotification Notification { get; set; }

        public async Task<IActionResult> OnPostAsync(int id)
        {
            string? token = Request.Cookies["AccessToken"];
            string? role = JwtHelper.GetRoleFromToken(token);
            if (string.IsNullOrEmpty(token)) return RedirectToPage("/Login");

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                string courseUrl = $"{_apiSettings.BaseUrl}/Course/{id}";
                var courseResponse = await _httpClient.GetAsync(courseUrl);
                if (!courseResponse.IsSuccessStatusCode)
                {
                    _logger.LogError($"Không tìm thấy khóa học: {courseResponse.StatusCode}");
                    return NotFound();
                }

                var courseJson = await courseResponse.Content.ReadAsStringAsync();
                Course = JsonSerializer.Deserialize<CourseResponseDto>(courseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new CourseResponseDto();

                string apiUrl = $"{_apiSettings.BaseUrl}/Course/toggle-deleted/{id}";

                HttpResponseMessage response = await _httpClient.PutAsync(apiUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    if (role == "Admin")
                    {
                        if (Course.isDeleted != true)
                        {
                            var notification = new CreateNotification
                            {
                                userId = Course.Mentor.UserId,
                                message = $"Your course titled '{Course.CourseName}' has been removed for violating our standards."
                            };

                            var content = new StringContent(JsonSerializer.Serialize(notification), System.Text.Encoding.UTF8, "application/json");

                            string notiApiUrl = $"{_apiSettings.BaseUrl}/Notification";
                            var notiResponse = await _httpClient.PostAsync(notiApiUrl, content);

                            if (!notiResponse.IsSuccessStatusCode)
                            {
                                _logger.LogError($"Gửi thông báo thất bại: {notiResponse.StatusCode}");
                            }
                        }
                        else 
                        {
                            var notification = new CreateNotification
                            {
                                userId = Course.Mentor.UserId,
                                message = $"Your course titled '{Course.CourseName}' has been restored after we reviewed the course again."
                            };

                            var content = new StringContent(JsonSerializer.Serialize(notification), System.Text.Encoding.UTF8, "application/json");

                            string notiApiUrl = $"{_apiSettings.BaseUrl}/Notification";
                            var notiResponse = await _httpClient.PostAsync(notiApiUrl, content);

                            if (!notiResponse.IsSuccessStatusCode)
                            {
                                _logger.LogError($"Gửi thông báo thất bại: {notiResponse.StatusCode}");
                            }
                        }
                        return RedirectToPage("/Admin/CourseManager/Index");
                    }
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