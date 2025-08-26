using final_project_fe.Dtos.Transaction;
using final_project_fe.Pages.Mentor.MentorPage;
using final_project_fe.Services;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Text;
using final_project_fe.Dtos.Users;
using System.Drawing;
using System.Net.Http.Headers;
using final_project_fe.Dtos.Notification;

namespace final_project_fe.Pages
{
    public class SuccessModel : PageModel
    {
        private readonly ILogger<SuccessModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private readonly PayOSService _payOSService;
        public SuccessModel(ILogger<SuccessModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient, PayOSService payOSService)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
            _payOSService = payOSService;
        }
        public string BaseUrl { get; set; }
        public TransactionDto Transaction { get; set; }
        public string CurrentUserId { get; set; }
        public async Task<IActionResult> OnGetAsync(long orderCode)
        {
            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }

                var paymentStatus = await _payOSService.GetPaymentStatus(orderCode);

                if (paymentStatus.IsPaid)
                {

                    Transaction = new TransactionDto
                    {
                        UserId = Guid.Parse(CurrentUserId),
                        Amount = paymentStatus.Amount,
                        Points = paymentStatus.Points,
                        OrderCode = orderCode.ToString(),
                        PaymentMethod = "payos",
                        Status = "Completed",
                        CreateAt = DateTime.UtcNow
                    };

                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var jsonContent = JsonSerializer.Serialize(Transaction);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");


                    var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/Transaction", content);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Add transaction success.");
                    }
                    else
                    {
                        string errorMessage = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Error while create transaction: {StatusCode}, Error message: {ErrorMessage}",
                                         response.StatusCode, errorMessage);
                    }

                    var queryParams = new Dictionary<string, string>
                    {
                        { "point", paymentStatus.Points.ToString() },
                        { "userId", Guid.Parse(CurrentUserId).ToString() }
                    };
                    var query = string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));

                    var response1 = await httpClient.PutAsync($"{_apiSettings.BaseUrl}/User/update-user-point?{query}", null);

                    if (response1.IsSuccessStatusCode)
                    {
                        var updatedUserJson = await response1.Content.ReadAsStringAsync();
                        var updatedUser = JsonSerializer.Deserialize<User>(updatedUserJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        TempData["SuccessMessage"] = "Payment successful!";
                        return RedirectToPage("/PointTransaction");
                    }

                    var notification = new CreateNotification
                    {
                        userId = Transaction.UserId,
                        message = $"You have successfully loaded {Transaction.Points} points into your account."
                    };

                    var notiContent = new StringContent(JsonSerializer.Serialize(notification), Encoding.UTF8, "application/json");
                    var notiApiUrl = $"{_apiSettings.BaseUrl}/Notification";
                    var notiResponse = await _httpClient.PostAsync(notiApiUrl, notiContent);

                    if (!notiResponse.IsSuccessStatusCode)
                    {
                        _logger.LogError($"Gửi thông báo thất bại: {notiResponse.StatusCode}");
                    }


                    return RedirectToPage("/PointTransaction");
                }
                else
                {
                    TempData["ErrorMessage"] = "Payment verification failed";
                    return RedirectToPage("/PointTransaction");
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "An error occurred while verifying payment";
                return RedirectToPage("/PointTransaction");
            }
        }
    }
}
