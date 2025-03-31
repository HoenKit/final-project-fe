using final_project_fe.Dtos.Post;
using System.Text.Json;
using System.Text;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using final_project_fe.Dtos;
using final_project_fe.Dtos.Comment;
using System.Security.Claims;
using final_project_fe.Dtos.Users;
using System.IdentityModel.Tokens.Jwt;

namespace final_project_fe.Pages
{
    public class UserPageModel : PageModel
    {
        private readonly ILogger<UserPageModel> _logger;
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;

        public UserPageModel(ILogger<UserPageModel> logger, HttpClient httpClient, IOptions<ApiSettings> apiSettings)
        {
            _logger = logger;
            _httpClient = httpClient;
            _apiSettings = apiSettings.Value;
        }

        [BindProperty]
        public PostDto NewPost { get; set; }

        // Use PageResult for paginated posts
        public PageResult<PostDto> Posts { get; set; }

        [BindProperty]
        public CommentCreateDto NewComment { get; set; } 
        public PageResult<CommentDto> Comments { get; set; }
        public Dictionary<int, List<CommentDto>> CommentsByPost { get; set; } = new();
        public Dictionary<int, List<PostFileDto>> PostFilesByPost { get; set; } = new();
        public int currentPage { get; set; }
        public async Task OnGetAsync(int? page)
        {
            string token = Request.Cookies["AccessToken"];
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            var userId = jsonToken.Claims
            .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                _logger.LogError("Không tìm thấy UserId của người dùng đã đăng nhập.");
                return;
            }
            currentPage = page ?? 1;
            CommentsByPost = new Dictionary<int, List<CommentDto>>();
          
            string userApiUrl = $"{_apiSettings.BaseUrl}/UserManager/";

            string postFileApiUrl = $"{_apiSettings.BaseUrl}/PostFile";


            try
            {
                // Gọi API lấy danh sách bài viết của người dùng đã đăng nhập
                string postsApiUrl = $"{_apiSettings.BaseUrl}/Post/user/{userId}";
                var response = await _httpClient.GetAsync(postsApiUrl);

                if (response.IsSuccessStatusCode)
                {
                    string postsJsonResponse = await response.Content.ReadAsStringAsync();
                    Posts = JsonSerializer.Deserialize<PageResult<PostDto>>(postsJsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<PostDto>(new List<PostDto>(), 0, 1, 10);
                }
                else
                {
                    _logger.LogError($"Lỗi khi lấy danh sách bài viết: {response.StatusCode}");
                    return;
                }
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

                    //Gọi API lấy Comments song song
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
                SubCategoryId = 3,
            };

            var jsonContent = JsonSerializer.Serialize(requestData);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/Post", content);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Bài viết đã được tạo thành công.");
                    return RedirectToPage("/UserPage");
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

        public async Task<IActionResult> OnPostCreateCommentAsync()
        {
            if (NewComment.Content == null || NewComment.PostId == null)
            {
                await OnGetAsync(currentPage);
                _logger.LogError("Invalid model state");
                return Page();
            }

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
                    _logger.LogError("Không tìm thấy UserId của người dùng đã đăng nhập.");
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
                    _logger.LogInformation("Comment đã được tạo thành công.");
                    return RedirectToPage("/UserPage");
                }
                else
                {
                    ModelState.AddModelError("", "Lỗi khi gửi bình luận.");
                }
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Đã xảy ra lỗi: {ex.Message}");
            }


            await OnGetAsync(currentPage);
            return Page();
        }
    }
}