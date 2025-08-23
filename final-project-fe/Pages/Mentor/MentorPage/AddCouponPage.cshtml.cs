using final_project_fe.Dtos;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Payment;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class AddCouponPageModel : PageModel
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _config;
        private readonly ApiSettings _apiSettings;
        public AddCouponPageModel(IHttpClientFactory httpClientFactory, IConfiguration config, IOptions<ApiSettings> apiSettings)
        {
            _httpClientFactory = httpClientFactory;
            _config = config;
            _apiSettings = apiSettings.Value;
        }

        [BindProperty]
        public AddCouponDto AddCoupon { get; set; } = new();
        public string BaseUrl { get; set; }
        public string? UserId { get; set; }
        public List<CouponDto> AvailableCoupons { get; set; } = new();
        public List<ListCourseDto> AvailableCourses { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            BaseUrl = _apiSettings.BaseUrl;
            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            // 🔹 Lấy userId & role từ JWT
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);

            var userIdClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier || c.Type == "sub");

            var roleClaim = jwtToken.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Role || c.Type == "role");

            if (userIdClaim == null) return RedirectToPage("/Login");

            // 🔹 Nếu role = mentor thì chặn
            var roleClaims = jwtToken.Claims
                .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                .Select(c => c.Value)
                .ToList();

            // 🔹 Nếu không có role Mentor thì chặn
            if (roleClaims == null || !roleClaims.Any(r => r.Equals("Mentor", StringComparison.OrdinalIgnoreCase)))
            {
                TempData["Error"] = "You do not have access to this page!";
                return RedirectToPage("/ErrorPage");
            }

            UserId = userIdClaim.Value;

            var client = _httpClientFactory.CreateClient();

            // 🔹 Get Mentor by userId
            var mentorUrl = $"{BaseUrl}/Mentor/get-by-user/{UserId}";
            var mentorRequest = new HttpRequestMessage(HttpMethod.Get, mentorUrl);
            mentorRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var mentorResponse = await client.SendAsync(mentorRequest);
            if (mentorResponse.IsSuccessStatusCode)
            {
                var mentorResp = await mentorResponse.Content.ReadFromJsonAsync<GetMentorDto>();

                if (mentorResp != null)
                {
                    // 🔹 Load Courses theo mentorId
                    var courseUrl = $"{BaseUrl}/Course?mentorId={mentorResp.MentorId}&statuses=Approved";
                    var courseRequest = new HttpRequestMessage(HttpMethod.Get, courseUrl);
                    courseRequest.Headers.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                    var courseResponse = await client.SendAsync(courseRequest);
                    if (courseResponse.IsSuccessStatusCode)
                    {
                        var courseResp = await courseResponse.Content.ReadFromJsonAsync<PageResult<ListCourseDto>>(
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        AvailableCourses = courseResp?.Items?.ToList() ?? new List<ListCourseDto>();
                    }
                }
            }

            // 🔹 Load Coupons
            var couponsUrl = $"{BaseUrl}/Coupon";
            var couponsRequest = new HttpRequestMessage(HttpMethod.Get, couponsUrl);
            couponsRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var couponsResponse = await client.SendAsync(couponsRequest);
            if (couponsResponse.IsSuccessStatusCode)
            {
                AvailableCoupons = await couponsResponse.Content.ReadFromJsonAsync<List<CouponDto>>()
                                   ?? new List<CouponDto>();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return RedirectToPage("/ErrorPage");

            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                return RedirectToPage("/Login");
            }

            BaseUrl = _apiSettings.BaseUrl;
            var mentorApiUrl = $"{BaseUrl}/Coupon/add-coupons";

            var client = _httpClientFactory.CreateClient();

            // 🔹 Tạo request thủ công
            var requestPost = new HttpRequestMessage(HttpMethod.Post, mentorApiUrl)
            {
                Content = JsonContent.Create(AddCoupon) // Serialize DTO thành JSON
            };

            // 🔹 Gắn token vào header
            requestPost.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

            var response = await client.SendAsync(requestPost);

            if (response.IsSuccessStatusCode)
            {
                TempData["Success"] = "Coupon added successfully!";
                return RedirectToPage("/Index");
            }
            else
            {
                var error = await response.Content.ReadAsStringAsync();
                ModelState.AddModelError("", $"Failed to add coupon: {error}");
                return Page();
            }
        }
    }
}