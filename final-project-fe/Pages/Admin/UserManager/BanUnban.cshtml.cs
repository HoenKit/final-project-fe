using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace final_project_fe.Pages.Admin.UserManager
{
    public class BanUnbanModel : PageModel
    {
        private readonly ILogger<BanUnbanModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;

        public BanUnbanModel(ILogger<BanUnbanModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }

        public User? User { get; set; }

        public async Task<IActionResult> OnPostAsync(Guid userId)
        {
            if (userId == Guid.Empty)
            {
                return BadRequest("Invalid User ID");
            }

            string? token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            string? role = JwtHelper.GetRoleFromToken(token);
            _logger.LogInformation($"User Role: {role}");

            if (role != "Admin")
            {
                return RedirectToPage("/Index");
            }

            string apiUrl = $"{_apiSettings.BaseUrl}/User/toggle-ban/{userId}";

            try
            {
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage response = await _httpClient.PutAsync(apiUrl, null);
                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    User = JsonSerializer.Deserialize<User>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    string emailSubject = User.IsBanned ? "Account Access Suspended" : "Account Access Restored";
                    
                    string isBanned = User.IsBanned ? "Banned" : "Unbanned";
                    TempData["SuccessMessage"] = $"User {User.Email} has been successfully {isBanned}.";
                    
                    string emailBody = $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Account Status Notification</title>
</head>
<body style='font-family: Arial, sans-serif; line-height: 1.6; color: #333; max-width: 600px; margin: 0 auto; padding: 20px;'>
    <div style='background-color: #f8f9fa; padding: 30px; border-radius: 8px; border-left: 4px solid {(User.IsBanned ? "#dc3545" : "#28a745")}; margin-bottom: 20px;'>
        <h2 style='color: {(User.IsBanned ? "#dc3545" : "#28a745")}; margin-top: 0;'>
            {(User.IsBanned ? "Account Access Suspended" : "Account Access Restored")}
        </h2>
        
        <p style='font-size: 16px; margin-bottom: 20px;'>Dear {User.Email},</p>
        
        <p style='font-size: 16px; margin-bottom: 20px;'>
            We are writing to inform you that your account status has been updated.
        </p>
        
        <div style='background-color: {(User.IsBanned ? "#fff5f5" : "#f0f9ff")}; padding: 20px; border-radius: 6px; margin: 20px 0;'>
            <p style='font-size: 16px; margin: 0; font-weight: bold;'>
                Status: <span style='color: {(User.IsBanned ? "#dc3545" : "#28a745")};'>
                    {(User.IsBanned ? "Account Suspended" : "Account Active")}
                </span>
            </p>
            <p style='font-size: 14px; margin: 10px 0 0 0; color: #666;'>
                Effective Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss} UTC
            </p>
        </div>
        
        {(User.IsBanned ?
                                @"<p style='font-size: 16px; margin-bottom: 20px;'>
                Your account has been temporarily suspended due to a violation of our terms of service. 
                During this period, you will not be able to access your account or use our services.
              </p>" :
                                @"<p style='font-size: 16px; margin-bottom: 20px;'>
                We are pleased to inform you that your account has been restored and you now have full access 
                to all features and services.
              </p>")}
        
        <div style='background-color: #e9ecef; padding: 15px; border-radius: 6px; margin: 20px 0;'>
            <p style='font-size: 14px; margin: 0; font-weight: bold;'>Next Steps:</p>
            <ul style='font-size: 14px; margin: 10px 0 0 0; padding-left: 20px;'>
                {(User.IsBanned ?
                                        @"<li>If you believe this action was taken in error, please contact our support team</li>
                      <li>Review our Terms of Service and Community Guidelines</li>
                      <li>Wait for further communication regarding account restoration</li>" :
                                        @"<li>You can now log in to your account using your regular credentials</li>
                      <li>All your data and settings have been preserved</li>
                      <li>Please ensure compliance with our Terms of Service going forward</li>")}
            </ul>
        </div>
        
        <p style='font-size: 16px; margin-bottom: 20px;'>
            If you have any questions or concerns regarding this decision, please don't hesitate to 
            contact our support team. We are here to assist you.
        </p>
        
        <div style='border-top: 1px solid #dee2e6; padding-top: 20px; margin-top: 30px;'>
            <p style='font-size: 14px; color: #666; margin-bottom: 10px;'>
                <strong>Contact Information:</strong><br>
                Email: c4t.travel@gmail.com<br>
                Phone: +1 (555) 123-4567<br>
                Hours: Monday - Friday, 9:00 AM - 6:00 PM EST
            </p>
            
            <p style='font-size: 14px; color: #666; margin-bottom: 20px;'>
                Thank you for your understanding.
            </p>
            
            <p style='font-size: 14px; color: #666; margin: 0;'>
                Best regards,<br>
                <strong>The Administration Team</strong><br>
                Phronesis Learning 
            </p>
        </div>
    </div>
    
    <div style='text-align: center; font-size: 12px; color: #999; margin-top: 20px;'>
        <p>This is an automated message. Please do not reply directly to this email.</p>
        <p>© 2025 Phronesis Learning. All rights reserved.</p>
    </div>
</body>
</html>";

                    var emailRequest = new
                    {
                        toEmail = User.Email,
                        subject = emailSubject,
                        body = emailBody
                    };

                    var emailContent = new StringContent(
                        JsonSerializer.Serialize(emailRequest),
                        System.Text.Encoding.UTF8,
                        "application/json");

                    try
                    {
                        string emailApi = $"{_apiSettings.BaseUrl}/Mail/send";
                        HttpResponseMessage emailResponse = await _httpClient.PostAsync(emailApi, emailContent);
                        if (!emailResponse.IsSuccessStatusCode)
                        {
                            _logger.LogWarning("Không thể gửi email thông báo tới người dùng.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Lỗi khi gửi email: {ex.Message}");
                    }

                    return RedirectToPage("./Index");
                }
                else
                {
                    _logger.LogError($"Lỗi API khi cập nhật trạng thái user: {response.StatusCode}");
                    return StatusCode((int)response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
                return StatusCode(500, "Internal Server Error");
            }
        }
    }
}
