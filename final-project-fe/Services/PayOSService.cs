using final_project_fe.Dtos.PayOS;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.Extensions.Options;
using Net.payOS.Types;
using Net.payOS;
using System.Net.Http;
using final_project_fe.Pages;

namespace final_project_fe.Services
{
    public class PayOSService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ApiSettings _apiSettings;
        private readonly PayOS _payOS;
        private readonly ILogger<PayOSService> _logger;
        public PayOSService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IOptions<ApiSettings> apiSettings, ILogger<PayOSService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _configuration = configuration;
            _apiSettings = apiSettings.Value;
            var clientId = configuration["PayOS:ClientId"];
            var apiKey = configuration["PayOS:ApiKey"];
            var checksumKey = configuration["PayOS:ChecksumKey"];

            _payOS = new PayOS(clientId, apiKey, checksumKey);
        }
        public async Task<PaymentResult> CreatePayOSPayment(int amount, int points, User user)
        {
            try
            {
                int orderCode = int.Parse(DateTimeOffset.Now.ToUnixTimeSeconds().ToString());

                // Tạo thông tin sản phẩm
                var item = new ItemData(
                    name: $"{points} Points",
                    quantity: 1,
                    price: amount
                );

                var items = new List<ItemData> { item };

                // Tạo thông tin thanh toán
                var paymentData = new PaymentData(
                    orderCode: orderCode,
                    amount: amount,
                    description: $"Phronesis {points} points",
                    items: items,
                    cancelUrl: $"{_apiSettings.FrontEndUrl}/Cancel",
                    returnUrl: $"{_apiSettings.FrontEndUrl}/Success",
                    buyerName: $"{user.UserMetaData.FirstName} {user.UserMetaData.LastName}",
                    buyerEmail: user.Email ?? "guest@example.com",
                    buyerPhone: "" 
                );

                var createPayment = await _payOS.createPaymentLink(paymentData);

                return new PaymentResult
                {
                    Success = true,
                    PaymentUrl = createPayment.checkoutUrl,
                    OrderCode = orderCode
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating PayOS payment link");
                return new PaymentResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<PaymentStatusResult> GetPaymentStatus(long orderCode)
        {
            try
            {
                var paymentInfo = await _payOS.getPaymentLinkInformation(orderCode);

                return new PaymentStatusResult
                {
                    Success = true,
                    Status = paymentInfo.status, 
                    Amount = paymentInfo.amount,
                    OrderCode = paymentInfo.orderCode,
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving payment status");
                return new PaymentStatusResult { Success = false, ErrorMessage = ex.Message };
            }
        }

    }
}

