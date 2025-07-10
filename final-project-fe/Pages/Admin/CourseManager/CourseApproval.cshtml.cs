using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Notification;
using final_project_fe.Dtos.Post;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace final_project_fe.Pages.Admin.CourseManager
{
    public class CourseApprovalModel : PageModel
    {
        private readonly ILogger<CourseApprovalModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public CourseApprovalModel(
            ILogger<CourseApprovalModel> logger,
            IOptions<ApiSettings> apiSettings,
            HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public GetCourseDto Course { get; set; }
        public CreateNotification Notification { get; set; }

        public async Task<IActionResult> OnPostAsync(int id, string status)
        {
            string? token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            string? role = JwtHelper.GetRoleFromToken(token);
            if (role != "Admin")
                return RedirectToPage("/Index");

            try
            {
                string courseUrl = $"{_apiSettings.BaseUrl}/Course/{id}";
                var courseRequest = new HttpRequestMessage(HttpMethod.Get, courseUrl);
                courseRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                var courseResponse = await _httpClient.SendAsync(courseRequest);
                if (!courseResponse.IsSuccessStatusCode)
                {
                    return NotFound();
                }

                var courseJson = await courseResponse.Content.ReadAsStringAsync();
                var courseRoot = JsonNode.Parse(courseJson);
                Course = courseRoot.Deserialize<GetCourseDto>(new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new GetCourseDto();

                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string apiUrl = $"{_apiSettings.BaseUrl}/Course/toggle-status?id={id}&status={status}";

                HttpResponseMessage response = await _httpClient.PutAsync(apiUrl, null);

                if (response.IsSuccessStatusCode)
                {
                    string message = status.ToLower() switch
                    {
                        "approved" => $"Your course titled '{Course.CourseName}' has been approved and is now live.",
                        "rejected" => $"Your course titled '{Course.CourseName}' has been rejected due to not meeting our requirements.",
                        _ => null
                    };

                    if (!string.IsNullOrEmpty(message))
                    {
                        var notification = new CreateNotification
                        {
                            userId = Course.Mentor.UserId,
                            message = message
                        };

                        var content = new StringContent(JsonSerializer.Serialize(notification), System.Text.Encoding.UTF8, "application/json");

                        string notiApiUrl = $"{_apiSettings.BaseUrl}/Notification";
                        var notiResponse = await _httpClient.PostAsync(notiApiUrl, content);

                        if (!notiResponse.IsSuccessStatusCode)
                        {
                            _logger.LogError($"Gửi thông báo thất bại: {notiResponse.StatusCode}");
                        }
                    }

                    TempData["SuccessMessage"] = $"Course has been {status} successfully";
                    return RedirectToPage("./ListPending");
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to {status.ToLower()} course";
                    return RedirectToPage("./ListPending");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"API error when updating course status: {ex.Message}");
                return StatusCode(500);
            }
        }
    }
}