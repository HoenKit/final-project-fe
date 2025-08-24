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
        public User CurrentUser { get; set; } = new();
        public async Task<IActionResult> OnGetAsync()
        {
            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Please login first";
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
                    TempData["ErrorMessage"] = "Please login first";
                    return Page();
                }

                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadJwtToken(token);

                // 🔍 Lấy Role từ token
                var roleClaims = jwtToken.Claims
                    .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                    .Select(c => c.Value)
                    .ToList();

                var userIdClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "nameid" || c.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
                {
                    TempData["ErrorMessage"] = "Unable to identify user.";
                    return Page();
                }

                if (roleClaims.Contains("Mentor"))
                {
                    return RedirectToPage("/Index");
                }

                Mentor.UserId = userId;

                // 🔹 Lấy thông tin người dùng từ API
                string apiUrl = $"{_apiSettings.BaseUrl}/User/GetUserById/{userId}";
                using var userRequest = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage userResponse = await _httpClient.SendAsync(userRequest);
                if (userResponse.IsSuccessStatusCode)
                {
                    var json = await userResponse.Content.ReadAsStringAsync();
                    CurrentUser = JsonSerializer.Deserialize<User>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new User();
                }
                else
                {
                    TempData["ErrorMessage"] = "Cannot fetch user information from API.";
                    return Page();
                }

                if (CurrentUser.UserMetaData?.Birthday == null)
                {
                    TempData["ErrorMessage"] = "Date of birth unknown.";
                    return Page();
                }
                var dateOfBirth = CurrentUser.UserMetaData.Birthday.Value;

                // 🔹 Validate ExpiredDate
                if (!ValidateExpiredDate(Mentor.IssueDate.Value, Mentor.ExpiredDate, dateOfBirth))
                {
                    TempData["ErrorMessage"] = "Invalid CCCD expiration date. Must be at least 7 years after IssueDate and follow age rules (25, 40, 60).";
                    return Page();
                }

                // 📤 Gọi API đăng ký mentor
                string mentorApiUrl = $"{_apiSettings.BaseUrl}/Mentor";
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

                if (Mentor.Signature != null && Mentor.Signature.Length > 0)
                {
                    var streamContent = new StreamContent(Mentor.Signature.OpenReadStream());
                    streamContent.Headers.ContentType = new MediaTypeHeaderValue(Mentor.Signature.ContentType);
                    formContent.Add(streamContent, "Signature", Mentor.Signature.FileName);
                }

                using var mentorRequest = new HttpRequestMessage(HttpMethod.Post, mentorApiUrl)
                {
                    Content = formContent
                };
                mentorRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                HttpResponseMessage mentorResponse = await _httpClient.SendAsync(mentorRequest);
                if (mentorResponse.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Mentor registration successful! Please log in again.";
                    return RedirectToPage("/Logout");
                }
                else
                {
                    var errorMsg = await mentorResponse.Content.ReadAsStringAsync();
                    ModelState.AddModelError(string.Empty, $"API Error: {errorMsg}");
                    return Page();
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Lỗi khi gọi API: {ex.Message}";
                ModelState.AddModelError(string.Empty, "An error occurred during registration.");
            }

            return RedirectToPage("/Index");
        }


        private bool ValidateExpiredDate(DateTime issueDate, DateTime? expiredDate, DateTime dateOfBirth)
        {
            if (!expiredDate.HasValue)
                return true; // cho phép để trống

            // 1. Ít nhất 7 năm sau ngày cấp
            var minValidDate = issueDate.AddYears(7);
            if (expiredDate.Value < minValidDate)
                return false;

            // 2. Theo mốc tuổi: 25, 40, 60
            var ageAtIssue = (issueDate - dateOfBirth).TotalDays / 365.25;
            DateTime maxExpiry;

            if (ageAtIssue < 25)
                maxExpiry = dateOfBirth.AddYears(25);
            else if (ageAtIssue < 40)
                maxExpiry = dateOfBirth.AddYears(40);
            else if (ageAtIssue < 58)
                maxExpiry = dateOfBirth.AddYears(60);
            else 
                maxExpiry = dateOfBirth.AddYears(60);

            if (expiredDate.Value > maxExpiry)
                return false;

            return true;
        }
    }
}

