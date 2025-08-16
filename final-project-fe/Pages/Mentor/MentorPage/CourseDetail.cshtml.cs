using final_project_fe.Dtos;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Lesson;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Module;
using final_project_fe.Dtos.Payment;
using final_project_fe.Dtos.Question;
using final_project_fe.Dtos.Reviews;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Web;

namespace final_project_fe.Pages.Mentor.MentorPage
{
    public class CourseDetailModel : PageModel
    {
        private readonly ILogger<CourseDetailModel> _logger;
        private readonly ApiSettings _apiSettings;
        private readonly HttpClient _httpClient;
        public CourseDetailModel(ILogger<CourseDetailModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
        {
            _logger = logger;
            _apiSettings = apiSettings.Value;
            _httpClient = httpClient;
        }
        public CourseResponseDto Course { get; set; } = new CourseResponseDto();
        public User MentorInfor {  get; set; } = new User();
        [BindProperty]
        public int? SelectedCouponId { get; set; }

        [BindProperty]
        public int? CourseId { get; set; }
        public string CurrentUserId { get; set; }
        public List<CouponDto> AvailableCoupons { get; set; } = new();
        public GetMentorDto Mentor { get; set; }
        public CourseReviewPageResult Reviews { get; set; } = new(new List<ReviewResponseDto>(), 0, 1, 3, 0, 0);

        [BindProperty]
        public UpdateReviewDto Review { get; set; } = new UpdateReviewDto();
        public List<ModuleWithLessonsDto> Modules { get; set; } = new List<ModuleWithLessonsDto>();
        public string BaseUrl { get; set; }
        public PageResult<CategoryDto> Categories { get; set; } = new(new List<CategoryDto>(), 0, 1, 10);
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";
        public async Task<IActionResult> OnGetAsync(int courseId, int? currentPage)
        {
            BaseUrl = _apiSettings.BaseUrl;
            Reviews.Page = currentPage ?? 1;
            
            //Lưu trang trước đấy
            const string sessionKey = "PageHistory";
            var history = HttpContext.Session.GetString(sessionKey);
            List<string> pageHistory;

            if (string.IsNullOrEmpty(history))
            {
                pageHistory = new List<string>();
            }
            else
            {
                pageHistory = JsonSerializer.Deserialize<List<string>>(history);
            }

            // Lấy URL hiện tại
            var currentUrl = HttpContext.Request.Path + HttpContext.Request.QueryString;

            // Chỉ thêm nếu khác trang cuối cùng
            if (pageHistory.Count == 0 || pageHistory.Last() != currentUrl)
            {
                pageHistory.Add(currentUrl);
            }

            // Lưu lại vào session
            HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(pageHistory));
            try
            {
                // Get Category
                var categoryUrl = new UriBuilder($"{BaseUrl}/Category");
                var categoryQuery = HttpUtility.ParseQueryString(string.Empty);
                categoryQuery["page"] = "1";
                categoryQuery["pageSize"] = "100";
                categoryUrl.Query = categoryQuery.ToString();

                var cateResponse = await _httpClient.GetAsync(categoryUrl.ToString());
                if (cateResponse.IsSuccessStatusCode)
                {
                    var categoryJson = await cateResponse.Content.ReadAsStringAsync();
                    Categories = JsonSerializer.Deserialize<PageResult<CategoryDto>>(categoryJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                        ?? new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);
                }
                else
                {
                    _logger.LogWarning("Không thể lấy danh mục. Status: " + cateResponse.StatusCode);
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
                // Get User by ID
                var userResponse = await _httpClient.GetAsync($"{BaseUrl}/User/GetUserById/{Mentor.UserId}");
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

                // Get Module and Lesson
                var response = await _httpClient.GetAsync($"{BaseUrl}/Module/get-all-module-by-course/{courseId}");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var modules = JsonSerializer.Deserialize<List<ModuleWithLessonsDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    if (modules != null)
                    {
                        foreach (var module in modules)
                        {
                            var lessonRes = await _httpClient.GetAsync($"{BaseUrl}/Lesson/get-all-lesson-by-module/{module.ModuleId}");
                            if (lessonRes.IsSuccessStatusCode)
                            {
                                var lessonJson = await lessonRes.Content.ReadAsStringAsync();
                                var lessons = JsonSerializer.Deserialize<List<LessonResponseDto>>(lessonJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                                module.Lessons = lessons ?? new();
                            }
                        }

                        Modules = modules;
                    }

                }

                string couponapiUrl = $"{_apiSettings.BaseUrl}/Coupon/by-course/{courseId}";
                var responses = await _httpClient.GetAsync(couponapiUrl);

                if (responses.IsSuccessStatusCode)
                {
                    var json = await responses.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        AvailableCoupons = JsonSerializer.Deserialize<List<CouponDto>>(json,
                            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while edit course");
            }

            return Page();
        }


        public async Task<IActionResult> OnPostAsync(int? courseId)
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                string token = Request.Cookies["AccessToken"];
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                var userId = jsonToken?.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    ModelState.AddModelError("", "Please login before purchasing.");
                    return RedirectToPage("/Login");
                }

                int finalCouponId = (SelectedCouponId.HasValue && SelectedCouponId.Value != 0) ? SelectedCouponId.Value : 11;

                var request = new BuyCourseRequest
                {
                    UserId = userId,
                    CourseId = CourseId ?? 0,
                    CouponId = finalCouponId
                };

                var api = $"{_apiSettings.BaseUrl}/Payment/buy-course";
                var res = await _httpClient.PostAsJsonAsync(api, request);

                if (res.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Purchase successful!";
                    return RedirectToPage("/UserCourse");
                }

                var errorContent = await res.Content.ReadAsStringAsync();

                if (errorContent.Contains("NotEnoughPoint", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "You do not have enough points. Please recharge.";
                    return RedirectToPage("/Transaction/Index");
                }

                ModelState.AddModelError("", "Purchase failed. Please try again.");
                return RedirectToPage("/UserCourse");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during course purchase");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again.";
                return Page();
            }
        }


        //Handler Update Review
        public async Task<IActionResult> OnPostUpdateReviewAsync(int courseId)
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

                // Đảm bảo CourseId được set đúng
                if (Review == null)
                {
                    TempData["ErrorMessage"] = "Review data is missing.";
                    return RedirectToPage(new { courseId = courseId });
                }

                // Set CourseId từ parameter nếu chưa có
                if (Review.CourseId == 0 || Review.CourseId != courseId)
                {
                    Review.CourseId = courseId;
                }

                // Set UserId từ token nếu chưa có
                if (Review.UserId == Guid.Empty && !string.IsNullOrEmpty(CurrentUserId))
                {
                    if (Guid.TryParse(CurrentUserId, out var userGuid))
                    {
                        Review.UserId = userGuid;
                    }
                }

                // Log để debug
                _logger.LogInformation($"Updating review - ReviewId: {Review.ReviewId}, CourseId: {Review.CourseId}, UserId: {Review.UserId}");

                var jsonContent = new StringContent(
                    JsonSerializer.Serialize(Review, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }),
                    Encoding.UTF8,
                    "application/json"
                );

                // Set Authorization header
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi API update Review
                var response = await _httpClient.PutAsync($"{BaseUrl}/Review", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                    var updateReview = JsonSerializer.Deserialize<UpdateReviewDto>(responseContent, options);

                    TempData["SuccessMessage"] = "Review updated successfully!";
                    return RedirectToPage(new { courseId = courseId });
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Update Review failed! Status: {response.StatusCode}, Error: {errorContent}");

                    // Parse error message if possible
                    string errorMessage = "Failed to update Review.";
                    try
                    {
                        var errorObj = JsonSerializer.Deserialize<JsonElement>(errorContent);
                        if (errorObj.TryGetProperty("message", out var msgElement))
                        {
                            errorMessage = msgElement.GetString() ?? errorMessage;
                        }
                        else if (errorObj.TryGetProperty("title", out var titleElement))
                        {
                            errorMessage = titleElement.GetString() ?? errorMessage;
                        }
                    }
                    catch
                    {
                        // If can't parse error, use default message
                        errorMessage = $"Failed to update Review. Status: {response.StatusCode}";
                    }

                    TempData["ErrorMessage"] = errorMessage;
                    return RedirectToPage(new { courseId = courseId });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error while updating Review for courseId: {courseId}");
                TempData["ErrorMessage"] = "An error occurred while updating the Review: " + ex.Message;
                return RedirectToPage(new { courseId = courseId });
            }
        }

        //Handler Delete Review
        public async Task<IActionResult> OnPostDeleteReviewAsync(int id, int courseId)
        {
            BaseUrl = _apiSettings.BaseUrl;
            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.PutAsync($"{_apiSettings.BaseUrl}/Review/toggle-deleted/{id}", null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Review deleted successfully.";
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Failed to delete Review. Status: {response.StatusCode}, Error: {error}");
                    TempData["ErrorMessage"] = "Failed to delete Review.";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while deleting review.");
                TempData["ErrorMessage"] = "An unexpected error occurred while deleting the review.";
            }

            // Redirect lại trang hiện tại với courseId
            return RedirectToPage(new { courseId = courseId });
        }
    }
}
