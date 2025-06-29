using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Net.Http.Headers;

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
        [BindProperty] public IFormFile CertificateFile { get; set; }


        public string UserFullName { get; set; }
        public string CourseName { get; set; }
        public string MentorSignature { get; set; }
        public async Task<IActionResult> OnGetAsync( string userId ,int courseId)
        {
            string token = Request.Cookies["AccessToken"];
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            var userIdClaim = jsonToken?.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            userId = userIdClaim;
            CourseId = courseId;

            var client = _clientFactory.CreateClient();

            var request = new HttpRequestMessage(HttpMethod.Get, $"{_apiSettings.BaseUrl}/User/{userIdClaim}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var userResp = await response.Content.ReadFromJsonAsync<UserDto>();
                if (userResp?.UserMetaData != null)
                {
                    UserFullName = $"{userResp.UserMetaData.FirstName} {userResp.UserMetaData.LastName}";
                }
            }
            // 2. Get Course Info
            var courseResp = await client.GetFromJsonAsync<CourseCertificateDto>($"{_apiSettings.BaseUrl}/Course/{courseId}");
            CourseName = courseResp?.courseName ?? "Unknown Course";

            // 3. Get Mentor Info
            var mentorResp = await client.GetFromJsonAsync<MentorInforDto>($"{_apiSettings.BaseUrl}/Mentor/by-course/{courseId}");
            if (mentorResp != null)
            {
                MentorSignature = !string.IsNullOrWhiteSpace(mentorResp.Signature)
                    ? mentorResp.Signature
                    : $"{mentorResp.FirstName} {mentorResp.LastName}";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (CertificateFile == null || CertificateFile.Length == 0)
            {
                return Page();
            }

            using var content = new MultipartFormDataContent();
            using var fileStream = CertificateFile.OpenReadStream();
            content.Add(new StreamContent(fileStream)
            {
                Headers = { ContentType = new MediaTypeHeaderValue("application/pdf") }
            }, "file", "certificate.pdf");

            content.Add(new StringContent(UserId), "UserId");
            content.Add(new StringContent(CourseId.ToString()), "CourseId");

            var client = _clientFactory.CreateClient();
            client.BaseAddress = new Uri(_apiSettings.BaseUrl);

            var response = await client.PostAsync($"{_apiSettings.BaseUrl}/certificate/upload", content); 

            if (response.IsSuccessStatusCode)
            {
                return RedirectToPage("/Success");
            }

            ModelState.AddModelError(string.Empty, "Upload failed");
            return Page();
        }
    }
}
