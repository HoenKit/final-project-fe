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
                var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                var formContent = new MultipartFormDataContent();
                formContent.Add(new StringContent(Mentor.UserId.ToString()), "UserId");
                formContent.Add(new StringContent(Mentor.Introduction ?? ""), "Introduction");
                formContent.Add(new StringContent(Mentor.JobTitle ?? ""), "JobTitle");
                formContent.Add(new StringContent(Mentor.FirstName ?? ""), "FirstName");
                formContent.Add(new StringContent(Mentor.LastName ?? ""), "LastName");
                formContent.Add(new StringContent(Mentor.StudyLevel ?? ""), "StudyLevel");
                formContent.Add(new StringContent(Mentor.CitizenID ?? ""), "CitizenID");
                formContent.Add(new StringContent(Mentor.IssuePlace ?? ""), "IssuePlace");
                formContent.Add(new StringContent(Mentor.IssueDate?.ToString("yyyy-MM-dd") ?? ""), "IssueDate");
                formContent.Add(new StringContent(Mentor.ExpiredDate?.ToString("yyyy-MM-dd") ?? ""), "ExpiredDate");
                formContent.Add(new StringContent(Mentor.CreateAt.ToString("o")), "CreateAt");
                formContent.Add(new StringContent(Mentor.UpdateAt.ToString("o")), "UpdateAt");

                if (Mentor.Signature != null && Mentor.Signature.Length > 0)
                {
                    var streamContent = new StreamContent(Mentor.Signature.OpenReadStream());
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(Mentor.Signature.ContentType);
                    formContent.Add(streamContent, "Signature", Mentor.Signature.FileName);
                }

                HttpResponseMessage response = await _httpClient.PostAsync(mentorApiUrl, formContent);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage("/Index");
                }

                _logger.LogError($"Lỗi API Đăng Ký: {response.StatusCode}");
                ModelState.AddModelError(string.Empty, "Đăng ký thất bại! Vui lòng kiểm tra lại thông tin.");
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

