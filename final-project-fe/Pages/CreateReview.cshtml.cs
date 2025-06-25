using final_project_fe.Dtos.Category;
using final_project_fe.Dtos;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Lesson;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Module;
using final_project_fe.Dtos.Reviews;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Reflection;
using System.Text.Json;
using System.Web;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace final_project_fe.Pages
{
    public class CreateReviewModel : PageModel
    {
        private readonly ILogger<CreateReviewModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        public CreateReviewModel(ILogger<CreateReviewModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }
        [BindProperty]
        public ReviewDto Review { get; set; } = new ReviewDto();
        public string CurrentUserId { get; set; }
        public string BaseUrl { get; set; }
        public CourseReviewPageResult Reviews { get; set; } = new(new List<ReviewResponseDto>(), 0, 1, 3, 0, 0);
        public User MentorInfor { get; set; } = new User();
        public GetMentorDto Mentor { get; set; }
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";
        public CourseResponseDto Course { get; set; } = new CourseResponseDto();
        public async Task<IActionResult> OnGetAsync(int courseId)
        {
            BaseUrl = _apiSettings.BaseUrl;
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
                // Get Course
                var courseResponse = await _httpClient.GetAsync($"{BaseUrl}/Course/{courseId}");
                if (!courseResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Can not get Course. Status: " + courseResponse.StatusCode);
                    TempData["ErrorMessage"] = "Can not get Course!";
                    return RedirectToPage("/Mentor/MentorPage/Index");
                }

                var courseJson = await courseResponse.Content.ReadAsStringAsync();
                Course = JsonSerializer.Deserialize<CourseResponseDto>(courseJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (Course == null)
                {
                    ModelState.AddModelError("", "Can not find Course.");
                    TempData["ErrorMessage"] = "Can not find Course!";
                    return RedirectToPage("/Mentor/MentorPage/Index");
                }

                Course.CourseId = courseId;

                if (Course.CoursesImage != null)
                {
                    Course.CoursesImage = ImageUrlHelper.AppendSasTokenIfNeeded(Course.CoursesImage, SasToken);
                }

                // Gọi API lấy thông tin mentor
                var mentorResponse = await _httpClient.GetAsync($"{BaseUrl}/Mentor/{Course.MentorId}");
                if (!mentorResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Can not get Mentor.");
                    ModelState.AddModelError("", "You are not Mentor");
                    return Page();
                }

                var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
                Mentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (Mentor == null)
                {
                    ModelState.AddModelError("", "Mentor does not exist.");
                    return Page();
                }

                // Get Review 
                int pageSize = 3;
                var reviewResponse = await _httpClient.GetAsync($"{BaseUrl}/Review/get-by-course/{courseId}?page={Reviews.Page}&pageSize={pageSize}");
                if (!reviewResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Can not get Reviews.");
                    return Page();
                }

                var reviewJson = await reviewResponse.Content.ReadAsStringAsync();
                Reviews = JsonSerializer.Deserialize<CourseReviewPageResult>(reviewJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Get User by ID
                var userResponse = await _httpClient.GetAsync($"{BaseUrl}/User/{Mentor.UserId}");
                if (!userResponse.IsSuccessStatusCode)
                {
                    _logger.LogError("Can not get Mentor.");
                    ModelState.AddModelError("", "You are not Mentor");
                    return Page();
                }

                var userJson = await userResponse.Content.ReadAsStringAsync();
                MentorInfor = JsonSerializer.Deserialize<User>(userJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (MentorInfor == null)
                {
                    ModelState.AddModelError("", "Mentor does not exist.");
                    return Page();
                }

                if (MentorInfor.UserMetaData.Avatar != null)
                {
                    MentorInfor.UserMetaData.Avatar = ImageUrlHelper.AppendSasTokenIfNeeded(MentorInfor.UserMetaData.Avatar, SasToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while load create review");
            }

            return Page();

        }

        public async Task<IActionResult> OnPostAsync(int courseId)
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
                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                Review.CourseId = courseId;
                Review.UserId = Guid.Parse(CurrentUserId);
                var jsonContent = JsonSerializer.Serialize(Review);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");


                var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/Review", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Add Review success.");
                    TempData["SuccessMessage"] = "Create review successful!";
                    return RedirectToPage("/Mentor/MentorPage/CourseDetail", new { courseId = courseId });
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Error while create review: {StatusCode}, Error message: {ErrorMessage}",
                                     response.StatusCode, errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while create review");
            }
            return RedirectToPage("/Mentor/MentorPage/CourseDetail", new { courseId = courseId });
        }
    }
}
