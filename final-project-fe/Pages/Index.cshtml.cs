using final_project_fe.Dtos;
using final_project_fe.Dtos.Comment;
using final_project_fe.Dtos.Post;
using final_project_fe.Dtos.Users;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace final_project_fe.Pages
{
	public class IndexModel : PageModel
	{
		private readonly ILogger<IndexModel> _logger;
		private readonly ApiSettings _apiSettings;
		private readonly HttpClient _httpClient;

		public IndexModel(ILogger<IndexModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient)
		{
			_logger = logger;
			_apiSettings = apiSettings.Value;
			_httpClient = httpClient;
		}
        [BindProperty]
        public CommentCreateDto NewComment { get; set; }
        public PageResult<CommentDto> Comments { get; set; }
		public PageResult<PostDto> Posts { get; set; }
		public Dictionary<int, List<CommentDto>> CommentsByPost { get; set; } = new();
		public Dictionary<int, List<PostFileDto>> PostFilesByPost { get; set; } = new();

		public async Task OnGetAsync(int postId, int? page)
		{
			int currentPage = page ?? 1;
			CommentsByPost = new Dictionary<int, List<CommentDto>>();
			// URL Post API
			string postsApiUrl = $"{_apiSettings.BaseUrl}/Post";

			// URL Comment API
			string commentsApiUrl = $"{_apiSettings.BaseUrl}/Comment?postId={postId}&page={currentPage}";

			string userApiUrl = $"{_apiSettings.BaseUrl}/UserManager/";

			string postFileApiUrl = $"{_apiSettings.BaseUrl}/PostFile";

			try
			{
				// 1️ Gọi API lấy danh sách Posts
				HttpResponseMessage postsResponse = await _httpClient.GetAsync(postsApiUrl);
				if (postsResponse.IsSuccessStatusCode)
				{
					string postsJsonResponse = await postsResponse.Content.ReadAsStringAsync();
					Posts = JsonSerializer.Deserialize<PageResult<PostDto>>(postsJsonResponse, new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					}) ?? new PageResult<PostDto>(new List<PostDto>(), 0, 1, 10);
				}
				else
				{
					_logger.LogError($"Lỗi API Post: {postsResponse.StatusCode}");
					return;
				}

				// 2️ Tạo danh sách task để gọi API song song
				var userTasks = new List<Task>();
				var postFileTasks = new List<Task>();
				var commentTasks = new List<Task>();

				foreach (var post in Posts.Items)
				{
					// Gọi API lấy User song song
					userTasks.Add(Task.Run(async () =>
					{
						try
						{
							HttpResponseMessage userResponse = await _httpClient.GetAsync(userApiUrl + post.UserId);
							if (userResponse.IsSuccessStatusCode)
							{
								var userJson = await userResponse.Content.ReadAsStringAsync();
								var apiResponse = JsonSerializer.Deserialize<ApiResponse<User>>(userJson, new JsonSerializerOptions
								{
									PropertyNameCaseInsensitive = true
								});
								post.User = apiResponse?.Result;
							}
						}
						catch (Exception ex)
						{
							_logger.LogError($"Lỗi khi lấy User {post.UserId}: {ex.Message}");
						}
					}));

					// Gọi API lấy PostFiles song song
					postFileTasks.Add(Task.Run(async () =>
					{
						try
						{
							HttpResponseMessage postFileResponse = await _httpClient.GetAsync(postFileApiUrl + "?postId=" + post.PostId);
							if (postFileResponse.IsSuccessStatusCode)
							{
								string postFileJson = await postFileResponse.Content.ReadAsStringAsync();
								var files = JsonSerializer.Deserialize<List<PostFileDto>>(postFileJson, new JsonSerializerOptions
								{
									PropertyNameCaseInsensitive = true
								}) ?? new List<PostFileDto>();

								lock (PostFilesByPost) // Đảm bảo thread-safe
								{
									PostFilesByPost[post.PostId] = files;
								}
							}
						}
						catch (Exception ex)
						{
							_logger.LogError($"Lỗi khi lấy PostFiles {post.PostId}: {ex.Message}");
						}
					}));

					// 🔹 Gọi API lấy Comments song song
					commentTasks.Add(Task.Run(async () =>
					{
						try
						{
							string apiUrl = $"{_apiSettings.BaseUrl}/Comment?postId={post.PostId}&page=1";
							HttpResponseMessage response = await _httpClient.GetAsync(apiUrl);
							if (response.IsSuccessStatusCode)
							{
								string jsonResponse = await response.Content.ReadAsStringAsync();
								var comments = JsonSerializer.Deserialize<PageResult<CommentDto>>(jsonResponse, new JsonSerializerOptions
								{
									PropertyNameCaseInsensitive = true
								})?.Items ?? new List<CommentDto>();

								lock (CommentsByPost) // Đảm bảo thread-safe
								{
									CommentsByPost[post.PostId] = (List<CommentDto>)comments;
								}

								// Gọi API lấy User của mỗi comment
								var commentUserTasks = comments.Select(async comment =>
								{
									try
									{
										HttpResponseMessage userResponse = await _httpClient.GetAsync(userApiUrl + comment.UserId);
										if (userResponse.IsSuccessStatusCode)
										{
											var userJson = await userResponse.Content.ReadAsStringAsync();
											var apiResponse = JsonSerializer.Deserialize<ApiResponse<User>>(userJson, new JsonSerializerOptions
											{
												PropertyNameCaseInsensitive = true
											});
											comment.User = apiResponse?.Result;
										}
									}
									catch (Exception ex)
									{
										_logger.LogError($"Lỗi khi lấy User {comment.UserId}: {ex.Message}");
									}
								});

								await Task.WhenAll(commentUserTasks);
							}
						}
						catch (Exception ex)
						{
							_logger.LogError($"Lỗi khi lấy comments {post.PostId}: {ex.Message}");
						}
					}));
				}

				// 3️ Chờ tất cả các task hoàn thành
				await Task.WhenAll(userTasks);
				await Task.WhenAll(postFileTasks);
				await Task.WhenAll(commentTasks);
			}
			catch (Exception ex)
			{
				_logger.LogError($"Lỗi khi gọi API: {ex.Message}");
			}
		}


		public async Task<IActionResult> OnPostAsync()
		{

            try
            {
                string token = Request.Cookies["AccessToken"];
                if (string.IsNullOrEmpty(token))
                {
                    return Unauthorized();
                }
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

                if (jsonToken == null)
                {
                    return Unauthorized();
                }
                var userId = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized();
                }

                var requestData = new
                {
                    UserId = userId,
                    PostId = NewComment.PostId,
                    Content = NewComment.Content,
                    ParentCommentId = NewComment.ParentCommentId
                };

                var jsonContent = JsonSerializer.Serialize(requestData);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/Comment", content);

                if (response.IsSuccessStatusCode)
                {
                    return RedirectToPage("/Index", new { id = NewComment.PostId });
                }
                else
                {
                    ModelState.AddModelError("", "Lỗi khi gửi bình luận.");
                    return Page();
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Đã xảy ra lỗi: {ex.Message}");
                return Page();
            }
        }
    }
}


