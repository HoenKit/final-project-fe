using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

using System.Drawing;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace final_project_fe.Pages
{
    public class MentorRegisterModel : PageModel
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MentorRegisterModel> _logger;
        private readonly ApiSettings _apiSettings;

        public MentorRegisterModel( HttpClient httpClient, ILogger<MentorRegisterModel> logger, IOptions<ApiSettings> apiSettings)
        {
            _httpClient = httpClient;
            _logger = logger;
            _apiSettings = apiSettings.Value;
        }

        [BindProperty]
        public CreateMentorDto Mentor { get; set; } = new CreateMentorDto();
        public async Task<IActionResult> OnGetAsync()
        {
            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                ModelState.AddModelError("", "Bạn chưa đăng nhập.");
                return RedirectToPage("/Login");
            }

            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            // 🔍 Lấy Role từ token
            var roleClaims = jwtToken.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            if (roleClaims.Contains("Mentor"))
            {
                return RedirectToPage("/Index");
            }
            return Page();

        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();
            try
            {
                var token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    ModelState.AddModelError("", "Bạn chưa đăng nhập.");
                    return Page();
                }

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // 🔍 Lấy Role từ token
                var roleClaims = jwtToken.Claims
                    .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                    .Select(c => c.Value)
                    .ToList();

                if (roleClaims.Contains("Mentor"))
                {
                    return RedirectToPage("/Index");
                }

                // ✅ Lấy UserId từ token
                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid" || c.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    ModelState.AddModelError("", "Không thể xác định người dùng.");
                    return Page();
                }

                Mentor.UserId = userId;


                // 📤 Gọi API đăng ký mentor
                string mentorApiUrl = $"{_apiSettings.BaseUrl}/Mentor";

                // Tạo Multipart form-data
                var formContent = new MultipartFormDataContent
{
    { new StringContent(Mentor.UserId.ToString()), "UserId" },
    { new StringContent(Mentor.Introduction ?? ""), "Introduction" },
    { new StringContent(Mentor.JobTitle ?? ""), "JobTitle" },
    { new StringContent(Mentor.FirstName ?? ""), "FirstName" },
    { new StringContent(Mentor.LastName ?? ""), "LastName" },
    { new StringContent(Mentor.StudyLevel ?? ""), "StudyLevel" },
    { new StringContent(Mentor.CitizenID ?? ""), "CitizenID" },
    { new StringContent(Mentor.IssuePlace ?? ""), "IssuePlace" },
    { new StringContent(Mentor.IssueDate?.ToString("yyyy-MM-dd") ?? ""), "IssueDate" },
    { new StringContent(Mentor.ExpiredDate?.ToString("yyyy-MM-dd") ?? ""), "ExpiredDate" },
    { new StringContent(Mentor.CreateAt.ToString("o")), "CreateAt" },
    { new StringContent(Mentor.UpdateAt.ToString("o")), "UpdateAt" }
};

                // Thêm file chữ ký nếu có
                if (Mentor.Signature != null && Mentor.Signature.Length > 0)
                {
                    var streamContent = new StreamContent(Mentor.Signature.OpenReadStream());
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(Mentor.Signature.ContentType);
                    formContent.Add(streamContent, "Signature", Mentor.Signature.FileName);
                }

                // Gửi request với token
                using var requestPost = new HttpRequestMessage(HttpMethod.Post, mentorApiUrl)
                {
                    Content = formContent
                };
                requestPost.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Thực thi request
                HttpResponseMessage response = await _httpClient.SendAsync(requestPost);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage("/Logout");
                }
                else
                {
                    var errorMsg = await response.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, $"API Error: {errorMsg}");
                    return Page();
                }
                
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi gọi API: {ex.Message}");
                ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi trong quá trình đăng ký.");
            }
            return RedirectToPage("/Index");
        }
    }
}

