using System.Buffers.Text;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Reflection.Emit;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;
using final_project_fe.Dtos;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Dtos.Module;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace final_project_fe.Pages
{
    public class UserProfileModel : PageModel
    {
        private readonly ILogger<UserProfileModel> _logger;
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;
        private readonly SignalrSetting _signalrSetting;
        
        public UserProfileModel(ILogger<UserProfileModel> logger, HttpClient httpClient, IOptions<ApiSettings> apiSettings, SignalrSetting signalrSetting)
        {
            _logger = logger;
            _httpClient = httpClient;
            _apiSettings = apiSettings.Value;
            _signalrSetting = signalrSetting;
        }
        [BindProperty]
        public IFormFile? Avatar { get; set; }
        public string BaseUrl { get; set; }
        public List<UserCertificateDto> Certificates { get; set; } = new List<UserCertificateDto>();
        public bool IsOwnUser { get; set; } = false;
        [BindProperty]
        public User Profile { get; set; } = new User();
        public string CurrentUserId { get; set; }
        [BindProperty]
        public int MentorId { get; set; }
        [BindProperty]
        public bool IsMentor { get; set; } = false;
        [BindProperty]
        public IFormFile? MentorCertificateFile { get; set; }

        [BindProperty]
        public string? MentorCertificateName { get; set; }
        public List<MentorCertificateDto> MentorCertificates { get; set; } = new List<MentorCertificateDto>();
        public PageResult<CategoryDto> Categories { get; set; } = new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);
        public PageResult<GetCourseDto> Courses { get; set; } = new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 6);
        public GetMentorDto? CurrentMentor { get; set; }

        public List<string> UserRoles { get; private set; } = new List<string>();
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";
        public async Task<IActionResult> OnGetAsync(string? userId, int? currentPage, int? categoryId, string? title, string? sortOption, string? language, string? level, decimal? minCost, decimal? maxCost, decimal? minRate, decimal? maxRate, string? status)
        {
            BaseUrl = _apiSettings.BaseUrl;
            if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

                    // Kiểm tra role mentor
                    IsMentor = jsonToken.Claims.Any(c =>
                        (c.Type == ClaimTypes.Role || c.Type == "role") &&
                        c.Value == "Mentor");

                    // Lấy UserRoles từ token
                    UserRoles = jsonToken.Claims
                        .Where(c => c.Type == ClaimTypes.Role || c.Type == "role")
                        .Select(c => c.Value)
                        .ToList();
                }

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // 🔹 XÁC ĐỊNH XEM CÓ PHẢI PROFILE CỦA CHÍNH MÌNH KHÔNG
                IsOwnUser = string.IsNullOrEmpty(userId) || userId == CurrentUserId;

                if (IsOwnUser)
                {
                    await LoadCurrentUserProfile(currentPage, categoryId, title, sortOption, language, level, minCost, maxCost, minRate, maxRate, status);
                    await LoadMentorCertificates(CurrentUserId);   // 🔥 thêm dòng này
                }
                else
                {
                    await LoadOtherUserProfile(userId, currentPage, categoryId, title, sortOption, language, level, minCost, maxCost, minRate, maxRate, status);
                    await LoadMentorCertificates(userId);         // 🔥 vẫn giữ dòng này
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unknown error in OnGetAsync.");
                TempData["ErrorMessage"] = "An unexpected error occurred.";
                return Page();
            }

            return Page();
        }

        private async Task LoadCurrentUserProfile(int? currentPage, int? categoryId, string? title, string? sortOption, string? language, string? level, decimal? minCost, decimal? maxCost, decimal? minRate, decimal? maxRate, string? status)
        {
            try
            {
                // Gọi API lấy thông tin user hiện tại
                _httpClient.DefaultRequestHeaders.Clear();
                if (Request.Cookies.TryGetValue("AccessToken", out var token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.GetAsync($"{BaseUrl}/User/GetUserById/{CurrentUserId}");

                _logger.LogInformation("API Response Status: {StatusCode}", response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Unable to get user information. Status: {StatusCode}", response.StatusCode);
                    TempData["ErrorMessage"] = $"Error calling API: {response.StatusCode}";
                    return;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("API Response Content: {Content}", jsonContent);

                // Kiểm tra xem response có rỗng không
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogError("Received empty response from API");
                    TempData["ErrorMessage"] = "Received empty response from server";
                    return;
                }

                // Kiểm tra xem response có phải JSON không
                if (!jsonContent.TrimStart().StartsWith("{") && !jsonContent.TrimStart().StartsWith("["))
                {
                    _logger.LogError("Response is not valid JSON: {Content}", jsonContent);
                    TempData["ErrorMessage"] = "Invalid response format from server";
                    return;
                }

                var userInfo = JsonSerializer.Deserialize<User>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                if (userInfo == null)
                {
                    _logger.LogError("Failed to deserialize user info");
                    TempData["ErrorMessage"] = "Can not find Profile!";
                    return;
                }

                Profile = userInfo;
                Profile.UserMetaData.Avatar = ImageUrlHelper.AppendSasTokenIfNeeded(Profile.UserMetaData.Avatar, SasToken);

                // Nếu là mentor thì load thêm mentor info + courses
                if (UserRoles.Contains("Mentor"))
                {
                    await LoadMentorInfo(CurrentUserId, currentPage, categoryId, title, sortOption, language, level, minCost, maxCost, minRate, maxRate, status);
                }
                await LoadUserCertificates(CurrentUserId);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing error while loading current user profile");
                TempData["ErrorMessage"] = "Error parsing server response. Please try again.";
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Network error while loading current user profile");
                TempData["ErrorMessage"] = "Network error. Please check your connection and try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while loading current user profile");
                TempData["ErrorMessage"] = "An unexpected error occurred while loading profile.";
            }
        }

        private async Task LoadOtherUserProfile(string userId, int? currentPage, int? categoryId, string? title, string? sortOption, string? language, string? level, decimal? minCost, decimal? maxCost, decimal? minRate, decimal? maxRate, string? status)
        {
            try
            {
                // 🔹 Nếu truyền userId khác CurrentUserId → load profile user đó
                _httpClient.DefaultRequestHeaders.Clear();
                if (Request.Cookies.TryGetValue("AccessToken", out var token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.GetAsync($"{BaseUrl}/User/GetUserById/{userId}");

                _logger.LogInformation("API Response Status for user {UserId}: {StatusCode}", userId, response.StatusCode);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Unable to get searched user information. Status: {StatusCode}", response.StatusCode);
                    TempData["ErrorMessage"] = $"Error calling API: {response.StatusCode}";
                    return;
                }

                var jsonContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("API Response Content for user {UserId}: {Content}", userId, jsonContent);

                // Kiểm tra xem response có rỗng không
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _logger.LogError("Received empty response from API for user {UserId}", userId);
                    TempData["ErrorMessage"] = "User not found";
                    return;
                }

                // Kiểm tra xem response có phải JSON không
                if (!jsonContent.TrimStart().StartsWith("{") && !jsonContent.TrimStart().StartsWith("["))
                {
                    _logger.LogError("Response is not valid JSON for user {UserId}: {Content}", userId, jsonContent);
                    TempData["ErrorMessage"] = "Invalid response format from server";
                    return;
                }

                var userInfo = JsonSerializer.Deserialize<User>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                if (userInfo == null)
                {
                    _logger.LogError("Failed to deserialize user info for user {UserId}", userId);
                    TempData["ErrorMessage"] = "Can not find searched user Profile!";
                    return;
                }

                Profile = userInfo;
             Profile.UserMetaData.Avatar = ImageUrlHelper.AppendSasTokenIfNeeded(Profile.UserMetaData.Avatar, SasToken);

                // 👉 Gọi thêm API check xem user đó có phải Mentor không
                await LoadMentorInfo(userId, currentPage, categoryId, title, sortOption, language, level, minCost, maxCost, minRate, maxRate, status);
                await LoadUserCertificates(userId);

                _logger.LogInformation("Successfully loaded profile for user: {UserId}", userId);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing error while loading other user profile for {UserId}", userId);
                TempData["ErrorMessage"] = "Error parsing server response. Please try again.";
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Network error while loading other user profile for {UserId}", userId);
                TempData["ErrorMessage"] = "Network error. Please check your connection and try again.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while loading other user profile for {UserId}", userId);
                TempData["ErrorMessage"] = "An unexpected error occurred while loading profile.";
            }
        }

        private async Task LoadMentorCertificates(string userId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                if (Request.Cookies.TryGetValue("AccessToken", out var token))
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.GetAsync($"{BaseUrl}/MentorCertificate/GetByUserId?userId={userId}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    if (!string.IsNullOrWhiteSpace(jsonContent))
                    {
                        MentorCertificates = JsonSerializer.Deserialize<List<MentorCertificateDto>>(jsonContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? new List<MentorCertificateDto>();
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to load mentor certificates. Status: {StatusCode}", response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mentor certificates");
            }
        }



        private async Task LoadUserCertificates(string userId)
        {
            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                if (Request.Cookies.TryGetValue("AccessToken", out var token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }

                var response = await _httpClient.GetAsync($"{BaseUrl}/User/userCertificate?userId={userId}");

                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();

                    if (!string.IsNullOrWhiteSpace(jsonContent) &&
                        (jsonContent.TrimStart().StartsWith("[") || jsonContent.TrimStart().StartsWith("{")))
                    {
                        Certificates = JsonSerializer.Deserialize<List<UserCertificateDto>>(jsonContent, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        }) ?? new List<UserCertificateDto>();
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to load certificates. Status: {StatusCode}, Response: {Response}",
                        response.StatusCode, await response.Content.ReadAsStringAsync());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading user certificates");
            }
        }

        private async Task LoadMentorInfo(string userId, int? currentPage, int? categoryId, string? title, string? sortOption, string? language, string? level, decimal? minCost, decimal? maxCost, decimal? minRate, decimal? maxRate, string? status)
        {
            try
            {
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                {
                    _logger.LogWarning("No token found in cookies while loading mentor info.");
                    return;
                }

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var mentorResponse = await _httpClient.GetAsync($"{BaseUrl}/Mentor/get-by-user/{userId}");

                if (mentorResponse.IsSuccessStatusCode)
                {
                    var mentorJson = await mentorResponse.Content.ReadAsStringAsync();

                    _logger.LogInformation("Mentor API Response for user {UserId}: {Content}", userId, mentorJson);

                    // Kiểm tra mentor response
                    if (!string.IsNullOrWhiteSpace(mentorJson) &&
                        (mentorJson.TrimStart().StartsWith("{") || mentorJson.TrimStart().StartsWith("[")))
                    {
                        CurrentMentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                        });

                        if (CurrentMentor != null)
                        {
                            MentorId = CurrentMentor.MentorId;

                            // Load courses
                            await LoadCoursesAsync(currentPage, categoryId, title, sortOption, language, level, minCost, maxCost, minRate, maxRate, status);
                        }
                    }
                }
                else
                {
                    _logger.LogInformation("User {UserId} is not a mentor. Status: {StatusCode}", userId, mentorResponse.StatusCode);
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing error while loading mentor info for {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading mentor info for {UserId}", userId);
            }
        }

        private async Task LoadCoursesAsync(int? currentPage, int? categoryId, string? title, string? sortOption, string? language, string? level, decimal? minCost, decimal? maxCost, decimal? minRate, decimal? maxRate, string? status)
        {
            try
            {
                // Kiểm tra MentorId thay vì CurrentMentor
                if (MentorId <= 0) return;

                var courseUrl = new UriBuilder($"{BaseUrl}/Course");
                var courseQuery = HttpUtility.ParseQueryString(string.Empty);

                // Basic pagination
                courseQuery["page"] = (currentPage ?? 1).ToString();
                courseQuery["pageSize"] = "8";

                // Filter by current mentor - sử dụng MentorId từ OnGetAsync
                courseQuery["mentorId"] = MentorId.ToString();

                // Filter by status = "Approved"
                courseQuery["statuses"] = "Approved";

                // Add optional filters - Đồng bộ với controller parameters
                if (!string.IsNullOrWhiteSpace(title))
                    courseQuery["title"] = title.Trim();

                if (categoryId.HasValue && categoryId.Value > 0)
                    courseQuery["CategoryId"] = categoryId.Value.ToString();  // Dùng CategoryId với C hoa

                if (!string.IsNullOrWhiteSpace(sortOption))
                    courseQuery["sortOption"] = sortOption;

                if (!string.IsNullOrWhiteSpace(language))
                    courseQuery["Language"] = language;  // Dùng Language với L hoa

                if (!string.IsNullOrWhiteSpace(level))
                    courseQuery["Level"] = level;  // Dùng Level với L hoa

                if (minCost.HasValue && minCost.Value >= 0)
                    courseQuery["MinCost"] = minCost.Value.ToString();  // Dùng MinCost với M hoa

                if (maxCost.HasValue && maxCost.Value >= 0)
                    courseQuery["MaxCost"] = maxCost.Value.ToString();  // Dùng MaxCost với M hoa

                if (minRate.HasValue && minRate.Value >= 0)
                    courseQuery["MinRate"] = minRate.Value.ToString();  // Dùng MinRate với M hoa

                if (maxRate.HasValue && maxRate.Value >= 0)
                    courseQuery["MaxRate"] = maxRate.Value.ToString();  // Dùng MaxRate với M hoa

                if (!string.IsNullOrWhiteSpace(status))
                    courseQuery["statuses"] = status;  // Dùng statuses như trong controller

                courseUrl.Query = courseQuery.ToString();
                _logger.LogInformation("Loading courses with URL: {Url}", courseUrl.ToString());

                if (Request.Cookies.TryGetValue("AccessToken", out var token) && !string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                var courseResponse = await _httpClient.GetAsync(courseUrl.ToString());

                if (courseResponse.IsSuccessStatusCode)
                {
                    var courseJson = await courseResponse.Content.ReadAsStringAsync();
                    Courses = JsonSerializer.Deserialize<PageResult<GetCourseDto>>(courseJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 8);

                    // Gắn SAS token vào mỗi course image
                    if (Courses?.Items != null)
                    {
                        foreach (var course in Courses.Items)
                        {
                            if (!string.IsNullOrWhiteSpace(course.CoursesImage))
                            {
                                course.CoursesImage = ImageUrlHelper.AppendSasTokenIfNeeded(course.CoursesImage, SasToken);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to load courses. Status: {StatusCode}, Response: {Response}",
                        courseResponse.StatusCode, await courseResponse.Content.ReadAsStringAsync());
                    Courses = new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 8);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading courses");
                Courses = new PageResult<GetCourseDto>(new List<GetCourseDto>(), 0, 1, 8);
            }
        }

        public async Task<IActionResult> OnPostUploadMentorCertificateAsync()
        {
            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Please login before Checkin.";
                RedirectToPage("/Login");
            }

            if (MentorCertificateFile == null || string.IsNullOrEmpty(MentorCertificateName))
            {
                TempData["ErrorMessage"] = "Please provide both certificate file and name.";
                return Page();
            }

            try
            {
                // Get MentorId from UserId using API
                var mentorId = await GetMentorIdByUserIdAsync(CurrentUserId);
                if (mentorId == null)
                {
                    TempData["ErrorMessage"] = "Mentor information not found for current user.";
                    return Page();
                }

                using var form = new MultipartFormDataContent();
                form.Add(new StringContent(mentorId.ToString()), "MentorId");
                form.Add(new StringContent(MentorCertificateName), "CertificateName");

                var fileContent = new StreamContent(MentorCertificateFile.OpenReadStream());
                fileContent.Headers.ContentType =
                    new System.Net.Http.Headers.MediaTypeHeaderValue(MentorCertificateFile.ContentType);
                form.Add(fileContent, "FileUrl", MentorCertificateFile.FileName);

                using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/MentorCertificate")
                {
                    Content = form
                };
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Mentor certificate uploaded successfully!";
                    await LoadMentorCertificates(CurrentUserId);
                    return RedirectToPage();
                }

                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Upload mentor certificate failed: {StatusCode} - {Response}",
                    response.StatusCode, errorContent);
                TempData["ErrorMessage"] = $"Failed to upload certificate: {errorContent}";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while uploading mentor certificate");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again later.";
                return Page();
            }
        }

        // Helper method to get MentorId by UserId
        private async Task<int?> GetMentorIdByUserIdAsync(string userId)
        {
            BaseUrl = _apiSettings.BaseUrl;
            var token = Request.Cookies["AccessToken"];
            if (string.IsNullOrEmpty(token))
            {
                TempData["ErrorMessage"] = "Please login before Checkin.";
                 RedirectToPage("/Login");
            }
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            if (jsonToken != null)
            {
                CurrentUserId = jsonToken.Claims
                    .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                UserRoles = jsonToken.Claims
                    .Where(c => c.Type == ClaimTypes.Role)
                    .Select(c => c.Value)
                    .ToList();
            }
            userId = CurrentUserId;
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/Mentor/get-by-user/{userId}");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                var response = await _httpClient.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    var jsonContent = await response.Content.ReadAsStringAsync();
                    var mentorData = JsonSerializer.Deserialize<GetMentorDto>(jsonContent, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    });
                    return mentorData?.MentorId;
                }

                _logger.LogWarning("Failed to get mentor by user ID: {StatusCode}", response.StatusCode);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting mentor by user ID");
                return null;
            }
        }

        //Update UserAvatar
        public async Task<IActionResult> OnPostUpdateAvatarAsync()
        {
            try
            {
                // Lấy token từ cookie
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                {
                    TempData["ErrorMessage"] = "Please login to continue.";
                    return RedirectToPage("/Login");
                }

                // Giải mã token để lấy UserId
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }

                if (string.IsNullOrEmpty(CurrentUserId))
                {
                    TempData["ErrorMessage"] = "Unable to identify user.";
                    return RedirectToPage("/Login");
                }

                // Kiểm tra file avatar
                if (Avatar == null || Avatar.Length == 0)
                {
                    TempData["ErrorMessage"] = "Please select an avatar file.";
                    return Page();
                }

                // Validate file type và size
                var allowedTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif" };
                if (!allowedTypes.Contains(Avatar.ContentType.ToLowerInvariant()))
                {
                    TempData["ErrorMessage"] = "Only JPG, JPEG, PNG, GIF files are allowed.";
                    return Page();
                }

                if (Avatar.Length > 5 * 1024 * 1024) // 5MB
                {
                    TempData["ErrorMessage"] = "File size cannot exceed 5MB.";
                    return Page();
                }

                // Tạo form data - QUAN TRỌNG: Sử dụng đúng tên field như API expect
                using var form = new MultipartFormDataContent();

                // Thêm file với tên field chính xác như trong DTO
                var fileContent = new StreamContent(Avatar.OpenReadStream());
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(Avatar.ContentType);
                form.Add(fileContent, "Avatar", Avatar.FileName); // Tên field phải là "Avatar" như trong UpdateAvatarDto

                // Set authorization header
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Gửi PUT request với userId trong query string (như API expect)
                var apiUrl = $"{_apiSettings.BaseUrl}/User/update-avatar?userId={CurrentUserId}";
                var response = await _httpClient.PutAsync(apiUrl, form);

                // Log response để debug
                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"API Response: Status={response.StatusCode}, Content={responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Avatar updated successfully!";
                    return RedirectToPage();
                }
                else
                {
                    // Log chi tiết lỗi
                    _logger.LogError($"Update avatar failed. Status: {response.StatusCode}, Content: {responseContent}");

                    // Parse error message từ response nếu có
                    var errorMessage = "Failed to update avatar.";
                    try
                    {
                        if (!string.IsNullOrEmpty(responseContent))
                        {
                            // Nếu API trả về JSON error
                            var errorObj = JsonSerializer.Deserialize<JsonElement>(responseContent);
                            if (errorObj.TryGetProperty("message", out var msgElement))
                            {
                                errorMessage = msgElement.GetString() ?? errorMessage;
                            }
                        }
                        else
                        {
                            errorMessage = responseContent;
                        }
                    }
                    catch
                    {
                        // Nếu không parse được JSON, dùng raw content
                        if (!string.IsNullOrEmpty(responseContent))
                            errorMessage = responseContent;
                    }

                    TempData["ErrorMessage"] = errorMessage;
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating avatar.");
                TempData["ErrorMessage"] = $"Unexpected error occurred: {ex.Message}";
                return Page();
            }
        }

        // Handler Update Profile
        public async Task<IActionResult> OnPostAsync()
        {
            if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                return RedirectToPage("/Login");

            BaseUrl = _apiSettings.BaseUrl;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }
                if (string.IsNullOrEmpty(CurrentUserId))
                {
                    TempData["ErrorMessage"] = "Unable to identify current user";
                    return Page();
                }

                // Kiểm tra model validation
                //if (!ModelState.IsValid)
                //{
                //    TempData["ErrorMessage"] = "Please correct the errors and try again";
                //    return Page();
                //}

                /*var form = new MultipartFormDataContent();
                form.Add(new StringContent(Profile.Email ?? ""), "Email");
                form.Add(new StringContent(Profile.Phone ?? ""), "Phone");
                form.Add(new StringContent(Profile.UserMetaData.FirstName ?? ""), "FirstName");
                form.Add(new StringContent(Profile.UserMetaData.LastName ?? ""), "LastName");
                form.Add(new StringContent(Profile.UserMetaData.Address ?? ""), "Address");
                form.Add(new StringContent(Profile.UserMetaData.Nationality ?? ""), "Nationality");
                form.Add(new StringContent(Profile.UserMetaData.Gender ?? ""), "Gender");
                form.Add(new StringContent(Profile.UserMetaData.FavouriteSubject ?? ""), "FavouriteSubject");
                form.Add(new StringContent(Profile.UserMetaData.Level ?? ""), "Level");
                form.Add(new StringContent(Profile.UserMetaData.Goals ?? ""), "Goals");
                form.Add(new StringContent(Profile.UserMetaData.Birthday.Value.ToString("MM-dd-yyyy")), "Birthday");*/

                var jsonContent = new StringContent(
                   JsonSerializer.Serialize(Profile.UserMetaData),
                   Encoding.UTF8,
                   "application/json"
               );

                // 🔹 Thêm token vào request
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var response = await _httpClient.PutAsync($"{BaseUrl}/User/Update/{CurrentUserId}", jsonContent);

                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();

                    // Kiểm tra response content có rỗng không
                    if (!string.IsNullOrEmpty(responseContent))
                    {
                        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                        var updateProfile = JsonSerializer.Deserialize<User>(responseContent, options);
                        if (updateProfile != null)
                        {
                            Profile = updateProfile;
                        }
                    }

                    TempData["SuccessMessage"] = "Profile updated successfully!";
                    return RedirectToPage(); 
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Update profile failed! Status: " + response.StatusCode);
                    TempData["ErrorMessage"] = "Failed to update profile: " + errorContent;
                    return Page();
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Network error while updating profile");
                TempData["ErrorMessage"] = "Network error occurred. Please check your connection and try again.";
                return Page();
            }
            catch (TaskCanceledException tcEx)
            {
                _logger.LogError(tcEx, "Request timeout while updating profile");
                TempData["ErrorMessage"] = "Request timeout. Please try again.";
                return Page();
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "JSON parsing error while updating profile");
                TempData["ErrorMessage"] = "Invalid response from server. Please try again.";
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while updating profile");
                TempData["ErrorMessage"] = "An unexpected error occurred. Please try again later.";
                return Page();
            }
        }
    }
}
