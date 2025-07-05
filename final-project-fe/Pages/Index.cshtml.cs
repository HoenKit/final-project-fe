using final_project_fe.Dtos;
using final_project_fe.Dtos.Category;
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
        private readonly SignalrSetting _signalrSetting;

        public IndexModel(ILogger<IndexModel> logger, IOptions<ApiSettings> apiSettings, HttpClient httpClient, SignalrSetting signalrSetting)
		{
			_logger = logger;
			_apiSettings = apiSettings.Value;
			_httpClient = httpClient;
            _signalrSetting = signalrSetting;
        }
        [BindProperty]
        public CommentCreateDto NewComment { get; set; }
        [BindProperty(SupportsGet = true)]
        public string Query { get; set; } = string.Empty;
        public PageResult<CommentDto> Comments { get; set; }
		public PageResult<PostDto> Posts { get; set; }
        public List<CategoryDto> Categories { get; set; } = new (); 

        [BindProperty]
        public PostCreateDto NewPost { get; set; }
        public Dictionary<int, List<CommentDto>> CommentsByPost { get; set; } = new();
		public Dictionary<int, List<PostFileDto>> PostFilesByPost { get; set; } = new();

        public string HubUrl { get; set; }
        public string BaseUrl { get; set; }
        public string CurrentUserId { get; set; }
        public int currentPage { get; set; }

        //Search Post
        public async Task<IActionResult> OnGetSearchPostAsync()
        {
            try
            {
                // Gọi API lấy danh sách post theo query
                string apiUrl = $"{_apiSettings.BaseUrl}/Post?title={Query}";
                var response = await _httpClient.GetAsync(apiUrl);

                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    Posts = JsonSerializer.Deserialize<PageResult<PostDto>>(jsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<PostDto>(new List<PostDto>(), 0, 1, 10);
                }


                else
                {
                    _logger.LogError($"Failed to fetch posts. Status Code: {response.StatusCode}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching posts");
            }

            return Page();
        }

        public async Task OnGetAsync(int? page)
		{
            if (!string.IsNullOrWhiteSpace(Query))
            {
                await OnGetSearchPostAsync();
                return;
            }

            int currentPage = page ?? 1;
			CommentsByPost = new Dictionary<int, List<CommentDto>>();

            //URL to Html
            HubUrl = _signalrSetting.HubUrl;
            BaseUrl = _apiSettings.BaseUrl;

            // URL Post API
            string postsApiUrl = $"{_apiSettings.BaseUrl}/Post";

			// URL Comment API
			string userApiUrl = $"{_apiSettings.BaseUrl}/User/GetUserById/";

			string postFileApiUrl = $"{_apiSettings.BaseUrl}/PostFile";

            string categoryApiUrl = $"{_apiSettings.BaseUrl}/Category?";
         

            try
			{

                // Lay Current User dang dang nhap

                string token = Request.Cookies["AccessToken"];
                var handler = new JwtSecurityTokenHandler();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Lỗi không tìm thấy token");
                    // Optionally, set a default or fallback value if token is missing
                    CurrentUserId = null; // or other default value
                }
                else
                {
                    var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                    if (jsonToken == null)
                    {
                        _logger.LogError("Lỗi không thể đọc token");
                        CurrentUserId = null; // or other default value
                    }
                    else
                    {
                        var userId = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                        if (userId != null)
                        {
                            CurrentUserId = userId;
                        }
                        else
                        {
                            _logger.LogError("Không tìm thấy userId trong token");
                            CurrentUserId = null; // or other default value
                        }
                    }
                }

                var categoryResponse = await _httpClient.GetAsync(categoryApiUrl);
                if (categoryResponse.IsSuccessStatusCode)
                {
                    string categoryJson = await categoryResponse.Content.ReadAsStringAsync();
                    var categoryResult = JsonSerializer.Deserialize<PageResult<CategoryDto>>(categoryJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    Categories = categoryResult?.Items?.ToList() ?? new List<CategoryDto>();
                }
                else
                {
                    _logger.LogError("Không thể lấy danh sách Category. Status: " + categoryResponse.StatusCode);
                    Categories = new List<CategoryDto>();
                }

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
								var apiResponse = JsonSerializer.Deserialize<User>(userJson, new JsonSerializerOptions
								{
									PropertyNameCaseInsensitive = true
								});
								post.User = apiResponse;
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

					// Gọi API lấy Comments song song
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
											var apiResponse = JsonSerializer.Deserialize<User>(userJson, new JsonSerializerOptions
											{
												PropertyNameCaseInsensitive = true
											});
											comment.User = apiResponse;
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

        //Create Post
        public async Task<IActionResult> OnPostAsync()
        {
            if (NewPost.Title == null || NewPost.Content == null)
            {
                await OnGetAsync(currentPage);
                _logger.LogError("Invalid model state");
                return Page();
            }

            string token = Request.Cookies["AccessToken"];
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            var userId = jsonToken.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;


            if (userId == null)
            {
                _logger.LogError("Không tìm thấy UserId của người dùng đã đăng nhập.");
                return Page();
            }

            var requestData = new
            {
                UserId = userId,
                Title = NewPost.Title,
                Content = NewPost.Content,
                /*CategoryId = 3,*/
                CategoryId = NewPost.CategoryId,
            };

            var jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/Post", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Bài viết đã được tạo thành công.");
                    return RedirectToPage("/Index");
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Lỗi khi tạo bài viết: {StatusCode}, Nội dung lỗi: {ErrorMessage}",
                                     response.StatusCode, errorMessage);
                }
            }
            catch (HttpRequestException httpEx)
            {
                _logger.LogError(httpEx, "Lỗi HTTP khi gửi yêu cầu tạo bài viết.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi tạo bài viết.");
            }

            await OnGetAsync(currentPage);
            return Page();
        }

    }
}


