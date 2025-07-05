using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace final_project_fe.Pages
{
    public class UserCourseModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ImageSettings _imagesettings;
        private readonly ApiSettings _apiSettings;
        public UserCourseModel(IHttpClientFactory httpClientFactory, IOptions<ApiSettings> apiSettings, IOptions<ImageSettings> imageSettings)
        {
            _httpClientFactory = httpClientFactory;
            _apiSettings = apiSettings.Value;
            _imagesettings = imageSettings.Value;
        }
        public List<UserCourseDto> Courses { get; set; } = new();
        public string ImageKey { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Status { get; set; }

        [BindProperty(SupportsGet = true)]
        public string UserId { get; set; }
        public async Task<IActionResult> OnGetAsync()
        {
            ImageKey = _imagesettings.ImageKey;
            try
            {
                // 🔐 Lấy token từ cookie
                string token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login"); // hoặc xử lý khác

                // 🔓 Giải mã token và lấy userId từ claim
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                var userIdClaim = jsonToken?.Claims
                    .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userIdClaim))
                    return Unauthorized();

                UserId = userIdClaim;
                var client = _httpClientFactory.CreateClient();

                var statusParam = string.IsNullOrEmpty(Status) ? "" : $"&status={Status}";
                var url = $"{_apiSettings.BaseUrl}/Course/status?userId={UserId}{statusParam}";

                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var jsonString = await response.Content.ReadAsStringAsync();
                    Courses = JsonSerializer.Deserialize<List<UserCourseDto>>(jsonString, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new List<UserCourseDto>();
                }
                else
                {
                    // 🛑 API trả lỗi
                    Courses = new List<UserCourseDto>();
                }
            }
            catch (Exception ex)
            {
                // 🐞 Ghi log nếu cần thiết
                Courses = new List<UserCourseDto>();
            }

            return Page();
        }

    }
}
