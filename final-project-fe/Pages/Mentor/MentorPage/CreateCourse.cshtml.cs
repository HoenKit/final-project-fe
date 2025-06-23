using final_project_fe.Dtos;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Courses;
using final_project_fe.Dtos.Mentors;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Buffers.Text;
using System.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Web;

namespace final_project_fe.Pages.Mentor.MentorPage
{
	public class CreateCourseModel : PageModel
	{
		private readonly ILogger<CreateCourseModel> _logger;
		private readonly ApiSettings _apiSettings;
		private readonly HttpClient _httpClient;
		public CreateCourseModel(ILogger<CreateCourseModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
		{
			_logger = logger;
			_apiSettings = apiSettings.Value;
			_httpClient = httpClient;
		}
		[BindProperty]
		public CourseDto Course { get; set; } = new();
		public string CurrentUserId { get; set; }
		public string BaseUrl { get; set; }
		public List<string> UserRoles { get; private set; }
		public PageResult<CategoryDto> Categories { get; set; } = new(new List<CategoryDto>(), 0, 1, 10);

		public async Task<IActionResult> OnGetAsync()
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

					UserRoles = jsonToken.Claims
						.Where(c => c.Type == ClaimTypes.Role)
						.Select(c => c.Value)
						.ToList();
				}

				if (UserRoles == null || !UserRoles.Contains("Mentor"))
					return RedirectToPage("/Index");

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi API lấy danh mục
                var categoryUrl = new UriBuilder($"{BaseUrl}/Category");
				var categoryQuery = HttpUtility.ParseQueryString(string.Empty);
				categoryQuery["page"] = "1";
				categoryQuery["pageSize"] = "100";
				categoryUrl.Query = categoryQuery.ToString();

				var response = await _httpClient.GetAsync(categoryUrl.ToString());
				if (response.IsSuccessStatusCode)
				{
					var categoryJson = await response.Content.ReadAsStringAsync();
					Categories = JsonSerializer.Deserialize<PageResult<CategoryDto>>(categoryJson,
						new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
						?? new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);
				}
				else
				{
					_logger.LogWarning("Không thể lấy danh mục. Status: " + response.StatusCode);
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Lỗi khi xử lý OnGetAsync.");
			}

			return Page();
		}


		public async Task<IActionResult> OnPostAsync()
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

					UserRoles = jsonToken.Claims
						.Where(c => c.Type == ClaimTypes.Role)
						.Select(c => c.Value)
						.ToList();
				}

				if (UserRoles == null || !UserRoles.Contains("Mentor"))
					return RedirectToPage("/Index");

                _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Gọi API lấy danh mục
                var categoryUrl = new UriBuilder($"{BaseUrl}/Category");
				var categoryQuery = HttpUtility.ParseQueryString(string.Empty);
				categoryQuery["page"] = "1";
				categoryQuery["pageSize"] = "100";
				categoryUrl.Query = categoryQuery.ToString();

				var response = await _httpClient.GetAsync(categoryUrl.ToString());
				if (response.IsSuccessStatusCode)
				{
					var categoryJson = await response.Content.ReadAsStringAsync();
					Categories = JsonSerializer.Deserialize<PageResult<CategoryDto>>(categoryJson,
						new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
						?? new PageResult<CategoryDto>(new List<CategoryDto>(), 0, 1, 10);
				}
				else
				{
					_logger.LogWarning("Không thể lấy danh mục. Status: " + response.StatusCode);
				}

				// Gọi API lấy thông tin mentor
				var mentorResponse = await _httpClient.GetAsync($"{BaseUrl}/Mentor/get-by-user/{CurrentUserId}");
				if (!mentorResponse.IsSuccessStatusCode)
				{
					_logger.LogError("Can not get Mentor.");
					ModelState.AddModelError("", "You are not Mentor");
					return Page();
				}

				var mentorJson = await mentorResponse.Content.ReadAsStringAsync();
				var mentor = JsonSerializer.Deserialize<GetMentorDto>(mentorJson, new JsonSerializerOptions
				{
					PropertyNameCaseInsensitive = true
				});

				if (mentor == null)
				{
					ModelState.AddModelError("", "Mentor does not exist.");
					return Page();
				}

				var form = new MultipartFormDataContent();
				form.Add(new StringContent(Course.CourseName ?? ""), "CourseName");			
				form.Add(new StringContent(Course.CategoryId.ToString()), "CategoryId");
				form.Add(new StringContent(mentor.MentorId.ToString()), "MentorId");

		/*		if (Course.CoursesImage != null)
				{
					var stream = Course.CoursesImage.OpenReadStream();
					var fileContent = new StreamContent(stream);
					fileContent.Headers.ContentType = new MediaTypeHeaderValue(Course.CoursesImage.ContentType);
					form.Add(fileContent, "CoursesImage", Course.CoursesImage.FileName);
				}
*/
				// Gửi request tạo khóa học
				var response1 = await _httpClient.PostAsync($"{BaseUrl}/Course", form);

				if (response1.IsSuccessStatusCode)
				{
					TempData["SuccessMessage"] = "Create Course success!";
					return RedirectToPage("/Mentor/MentorPage/MyCourses");
				}
				else
				{
					_logger.LogError("Create Course failed!. Status: " + response1.StatusCode);
					ModelState.AddModelError("", "Create Course failed!");
					TempData["ErrorMessage"] = "Create Course failed!";
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while create course");
				ModelState.AddModelError("", "Error");
				TempData["ErrorMessage"] = "An error occurred while creating the course.";
			}

			return await OnGetAsync();
		}

	}
}
