using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace final_project_fe.Pages
{
    public class GenerateCertificateModel : PageModel
    {
        private readonly IHttpClientFactory _clientFactory;

        private readonly ApiSettings _apiSettings;

        public GenerateCertificateModel(IHttpClientFactory clientFactory, IOptions<ApiSettings> apiSettings)
        {
            _clientFactory = clientFactory;
            _apiSettings = apiSettings.Value;
        }

        [BindProperty] public int CourseId { get; set; }
        [BindProperty] public string UserId { get; set; }
        public bool IsCurrentUser { get; set; }
        
        public string UserFullName { get; set; }
        public string CourseName { get; set; }
        public string MentorSignature { get; set; }
        public string MentorFullName { get; set; }
        public string BaseUrl { get; set; }
        public async Task<IActionResult> OnGetAsync(string userId, int courseId)
        {
            string token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            var currentUserId = jsonToken?.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            IsCurrentUser = currentUserId == userId;
            UserId = userId;
            CourseId = courseId;
            BaseUrl = _apiSettings.BaseUrl;

            // 🔑 SAS token (nên config trong appsettings)
            string sasToken = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";

            var client = _clientFactory.CreateClient();

            // 🔹 1. Kiểm tra trạng thái học tập
            var courseCheckRequest = new HttpRequestMessage(HttpMethod.Get, $"{_apiSettings.BaseUrl}/Learning/UserCourse?userId={userId}&courseId={courseId}");
            courseCheckRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var courseCheckResp = await client.SendAsync(courseCheckRequest);

            if (!courseCheckResp.IsSuccessStatusCode)
                return RedirectToPage("/ErrorPage");

            var userCourse = await courseCheckResp.Content.ReadFromJsonAsync<UserCourseDto>();
            if (userCourse == null || !string.Equals(userCourse.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ErrorMessage"] = $"You have not completed this course.";
                return RedirectToPage("/UserCourse");
            }

            // 🔹 2. Lấy thông tin user
            var requestUser = new HttpRequestMessage(HttpMethod.Get, $"{_apiSettings.BaseUrl}/User/GetUserById/{userId}");
            requestUser.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var userResponse = await client.SendAsync(requestUser);

            if (userResponse.IsSuccessStatusCode)
            {
                var userDto = await userResponse.Content.ReadFromJsonAsync<UserDto>();
                if (userDto?.UserMetaData != null)
                    UserFullName = RemoveDiacritics($"{userDto.UserMetaData.FirstName} {userDto.UserMetaData.LastName}");
            }

            // 🔹 3. Lấy thông tin khóa học
            var courseRequest = new HttpRequestMessage(HttpMethod.Get, $"{_apiSettings.BaseUrl}/Course/{courseId}");
            courseRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var courseResponse = await client.SendAsync(courseRequest);

            if (courseResponse.IsSuccessStatusCode)
            {
                var courseDto = await courseResponse.Content.ReadFromJsonAsync<CourseCertificateDto>();
                CourseName = courseDto?.courseName ?? "Unknown Course";
            }

            // 🔹 4. Lấy thông tin mentor
            var mentorRequest = new HttpRequestMessage(HttpMethod.Get, $"{_apiSettings.BaseUrl}/Mentor/by-course/{courseId}");
            mentorRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var mentorResponse = await client.SendAsync(mentorRequest);

            if (mentorResponse.IsSuccessStatusCode)
            {
                var mentorDto = await mentorResponse.Content.ReadFromJsonAsync<MentorInforDto>();

                if (!string.IsNullOrWhiteSpace(mentorDto?.Signature))
                {
                    MentorSignature = mentorDto.Signature.Contains("sig=")
                        ? mentorDto.Signature
                        : $"{mentorDto.Signature}?{sasToken}";
                }
                else
                {
                    MentorSignature = RemoveDiacritics($"{mentorDto?.FirstName} {mentorDto?.LastName}");
                }

                MentorFullName = RemoveDiacritics($"{mentorDto?.FirstName} {mentorDto?.LastName}");
            }

            return Page();
        }



        private static string RemoveDiacritics(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
