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
using System.Buffers.Text;
using final_project_fe.Dtos.Category;

namespace final_project_fe.Pages
{
    public class UserPageModel : PageModel
    {
        private readonly ILogger<UserPageModel> _logger;
        private readonly HttpClient _httpClient;
        private readonly ApiSettings _apiSettings;
        private readonly SignalrSetting _signalrSetting;

        public UserPageModel(ILogger<UserPageModel> logger, HttpClient httpClient, IOptions<ApiSettings> apiSettings, SignalrSetting signalrSetting)
        {
            _logger = logger;
            _httpClient = httpClient;
            _apiSettings = apiSettings.Value;
            _signalrSetting = signalrSetting;
        }

        [BindProperty]
        public PostCreateDto NewPost { get; set; }

        // Use PageResult for paginated posts
        public PageResult<PostDto> Posts { get; set; }
        public List<CategoryDto> Categories { get; set; } = new();
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";

        [BindProperty]
        public CommentCreateDto NewComment { get; set; } 
        public PageResult<CommentDto> Comments { get; set; }
        public User Profile { get; set; } = new();
        public Dictionary<int, List<CommentDto>> CommentsByPost { get; set; } = new();
        public Dictionary<int, List<PostFileDto>> PostFilesByPost { get; set; } = new();
        public int currentPage { get; set; }
        public string CurrentUserId { get; set; }
        public string HubUrl { get; set; }
        public string BaseUrl { get; set; }

        public async Task OnGetAsync(int? page)
        {
            int currentPage = page ?? 1;
            CommentsByPost = new Dictionary<int, List<CommentDto>>();

            //URL to Html
            HubUrl = _signalrSetting.HubUrl;
            BaseUrl = _apiSettings.BaseUrl;



            // URL Comment API
            string userApiUrl = $"{_apiSettings.BaseUrl}/User/GetUserById/";

            string postFileApiUrl = $"{_apiSettings.BaseUrl}/PostFile";

            string categoryApiUrl = $"{_apiSettings.BaseUrl}/Category?";


            try
            {

                // Lay Current User dang dang nhap
                string token = Request.Cookies["AccessToken"];
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                var userId = jsonToken.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                // URL Post by UserId API
                string postsApiUrl = $"{_apiSettings.BaseUrl}/Post?userId={userId}";

                var response = await _httpClient.GetAsync(postsApiUrl);
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Lỗi không tìm thấy token");
                    // Optionally, set a default or fallback value if token is missing
                    CurrentUserId = null; // or other default value
                }
                else
                {
                    if (jsonToken == null)
                    {
                        _logger.LogError("Lỗi không thể đọc token");
                        CurrentUserId = null; // or other default value
                    }
                    else
                    {
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

                    CurrentUserId = userId;
                    var profileResponse = await _httpClient.GetAsync(userApiUrl + userId);
                    if (profileResponse.IsSuccessStatusCode)
                    {
                        var userJson = await profileResponse.Content.ReadAsStringAsync();
                        var apiResponse = JsonSerializer.Deserialize<User>(userJson, new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });

                        if (apiResponse != null)
                            Profile = apiResponse;
                    }
                    else
                    {
                        _logger.LogError("Unable to get user profile information. Status: " + profileResponse.StatusCode);
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

                    if (Posts?.Items != null)
                    {
                        foreach (var post in Posts.Items)
                        {
                            foreach (var postFile in post.PostFiles)
                            {
                                if (!string.IsNullOrWhiteSpace(postFile.FileUrl))
                                {
                                    postFile.FileUrl = ImageUrlHelper.AppendSasTokenIfNeeded(postFile.FileUrl, SasToken);
                                }
                            }
                        }
                    }
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
    }
}