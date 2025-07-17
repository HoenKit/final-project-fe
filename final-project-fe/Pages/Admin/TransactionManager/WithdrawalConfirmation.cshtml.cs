using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Notification;
using final_project_fe.Pages.Admin.CourseManager;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json.Nodes;
using System.Text.Json;

namespace final_project_fe.Pages.Admin.TransactionManager
{
    public class WithdrawalConfirmationModel : PageModel
    {
        private readonly ILogger<WithdrawalConfirmationModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public WithdrawalConfirmationModel(
            ILogger<WithdrawalConfirmationModel> logger,
            IOptions<ApiSettings> apiSettings,
            HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public async Task<IActionResult> OnPostAsync(int id, string status, Guid userId, decimal points)
        {
            string? token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            string? role = JwtHelper.GetRoleFromToken(token);
            if (role != "Admin")
                return RedirectToPage("/Index");

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                string apiUrl = $"{_apiSettings.BaseUrl}/Withdraw/status/{id}?status={status}";

                HttpResponseMessage response = await _httpClient.PutAsync(apiUrl, null);

                if (response.IsSuccessStatusCode)
                {
                    string message = status.ToLower() switch
                    {
                        "accepted" => $"Request to withdraw '{points.ToString("N0")}' Points accepted.",
                        "refused" => $"The request to withdraw '{points}' Points has been denied.",
                        _ => null
                    };

                    if (!string.IsNullOrEmpty(message))
                    {
                        var notification = new CreateNotification
                        {
                            userId = userId,
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

                    TempData["SuccessMessage"] = $"Request has been {status} successfully";
                    return RedirectToPage("./WithdrawalRequest");
                }
                else
                {
                    TempData["ErrorMessage"] = $"Failed to {status.ToLower()} request";
                    return RedirectToPage("./WithdrawalRequest");
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
