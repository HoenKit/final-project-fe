using final_project_fe.Dtos.Transaction;
using final_project_fe.Services;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace final_project_fe.Pages
{
    public class CancelModel : PageModel
    {
        private readonly ILogger<CancelModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        private readonly PayOSService _payOSService;
        public CancelModel(ILogger<CancelModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient, PayOSService payOSService)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
            _payOSService = payOSService;
        }
        public string BaseUrl { get; set; }
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

                if (paymentStatus.Status == "CANCELLED")
                {

                    var transaction = new TransactionDto
                    {
                        UserId = Guid.Parse(CurrentUserId),
                        Amount = paymentStatus.Amount,
                        Points = paymentStatus.Points,
                        OrderCode = orderCode.ToString(),
                        PaymentMethod = "payos",
                        Status = "Cancel",
                        CreateAt = DateTime.UtcNow
                    };

                    var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    var jsonContent = JsonSerializer.Serialize(transaction);
                    var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");


                    var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/Transaction", content);

                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Add transaction success.");
                        TempData["SuccessMessage"] = "Cancel Payment successful!";
                        return RedirectToPage("/PointTransaction");
                    }
                    else
                    {
                        string errorMessage = await response.Content.ReadAsStringAsync();
                        _logger.LogError("Error while create transaction: {StatusCode}, Error message: {ErrorMessage}",
                                         response.StatusCode, errorMessage);
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
