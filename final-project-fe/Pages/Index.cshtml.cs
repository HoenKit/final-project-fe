using final_project_fe.Dtos;
using final_project_fe.Dtos.Category;
using final_project_fe.Dtos.Comment;
using final_project_fe.Dtos.Post;
using final_project_fe.Dtos.Users;
using final_project_fe.Dtos.WorkShop;
using final_project_fe.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Web;
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
        public PageResult<CategoryDto> Categories { get; set; } = new(new List<CategoryDto>(), 0, 1, 10);
        public string SasToken { get; set; } = "sp=r&st=2025-05-28T06:11:09Z&se=2026-01-01T14:11:09Z&spr=https&sv=2024-11-04&sr=c&sig=YdDYGbzpNp4XPSKVVDM0bb411XOEPgA8b0i2PFCfc1c%3D";

        [BindProperty]
        public PostCreateDto NewPost { get; set; } = new PostCreateDto();
        public Dictionary<int, List<CommentDto>> CommentsByPost { get; set; } = new();
        public Dictionary<int, List<PostFileDto>> PostFilesByPost { get; set; } = new();
        public User Profile { get; set; } = new();

        [BindProperty]
        public List<IFormFile> PostFileLinks { get; set; } = new List<IFormFile>();
        public string HubUrl { get; set; }
        public string BaseUrl { get; set; }
        public string CurrentUserId { get; set; }
        public int currentPage { get; set; }
        public PageResult<WorkShopDto>? WorkShops { get; set; } 
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
            string workshopApiUrl = $"{_apiSettings.BaseUrl}/WorkShop";

            try
            {

                // Lay Current User dang dang nhap

                string token = Request.Cookies["AccessToken"];
                var handler = new JwtSecurityTokenHandler();
                if (string.IsNullOrEmpty(token))
                {
                    _logger.LogError("Token not found error");
                    // Optionally, set a default or fallback value if token is missing
                    CurrentUserId = null; // or other default value
                }
                else
                {
                    var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                    if (jsonToken == null)
                    {
                        _logger.LogError("Error cannot read token");
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
                            _logger.LogError("UserId not found in token");
                            CurrentUserId = null; // or other default value
                        }
                    }
                }
                if (!string.IsNullOrEmpty(CurrentUserId))
                {
                    try
                    {
                        // Gọi API để lấy thông tin Profile của user hiện tại
                        HttpResponseMessage profileResponse = await _httpClient.GetAsync(userApiUrl + CurrentUserId);
                        if (profileResponse.IsSuccessStatusCode)
                        {
                            var profileJson = await profileResponse.Content.ReadAsStringAsync();
                            Profile = JsonSerializer.Deserialize<User>(profileJson, new JsonSerializerOptions
                            {
                                PropertyNameCaseInsensitive = true
                            }) ?? new User();
                        }
                        else
                        {
                            _logger.LogWarning($"Không thể lấy Profile user. Status: {profileResponse.StatusCode}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Lỗi khi lấy Profile user: {ex.Message}");
                    }
                }

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
                // API danh sach WorkShop"
                HttpResponseMessage workshopResponse = await _httpClient.GetAsync(workshopApiUrl);
                if (workshopResponse.IsSuccessStatusCode)
                {
                    string workshopJsonResponse = await workshopResponse.Content.ReadAsStringAsync();
                    WorkShops = JsonSerializer.Deserialize<PageResult<WorkShopDto>>(workshopJsonResponse, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    }) ?? new PageResult<WorkShopDto>(new List<WorkShopDto>(), 0, 1, 10);
                }
                else
                {
                    _logger.LogError($"Lỗi API Post: {postsResponse.StatusCode}");
                    return;
                }
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

        public async Task<IActionResult> OnGetPostByIdAsync(int postId)
        {
            try
            {
                string postApiUrl = $"{_apiSettings.BaseUrl}/Post/{postId}";
                var response = await _httpClient.GetAsync(postApiUrl);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError($"Không thể lấy Post theo Id. Status: {response.StatusCode}");
                    return NotFound();
                }

                string postJson = await response.Content.ReadAsStringAsync();
                var post = JsonSerializer.Deserialize<PostCreateDto>(postJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (post == null)
                {
                    _logger.LogWarning("Không tìm thấy Post theo Id.");
                    return NotFound();
                }

                return new JsonResult(post);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Lỗi khi lấy Post theo Id: {ex.Message}");
                return StatusCode(500);
            }
        }

        //Create Post
        public async Task<IActionResult> OnPostAsync()
        {
            // Kiểm tra dữ liệu đầu vào
            if (string.IsNullOrWhiteSpace(NewPost.Title) || string.IsNullOrWhiteSpace(NewPost.Content))
            {
                await OnGetAsync(currentPage);
                _logger.LogError("Tiêu đề hoặc nội dung bị thiếu.");
                return Page();
            }

            // Lấy UserId từ AccessToken
            string token = Request.Cookies["AccessToken"];
            var handler = new JwtSecurityTokenHandler();
            var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
            var userId = jsonToken?.Claims
                .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (userId == null)
            {
                _logger.LogError("Không tìm thấy UserId của người dùng đã đăng nhập.");
                return Page();
            }

            var form = new MultipartFormDataContent
            {
                { new StringContent(userId), "UserId" },
                { new StringContent(NewPost.Title), "Title" },
                { new StringContent(NewPost.Content), "Content" },
                { new StringContent(NewPost.CategoryId.ToString()), "CategoryId" }
            };

            if (NewPost.PostFileLinks != null && NewPost.PostFileLinks.Count > 0)
            {
                _logger.LogInformation($"Đang xử lý {NewPost.PostFileLinks.Count} file(s)");

                foreach (var file in NewPost.PostFileLinks)
                {
                    if (file != null && file.Length > 0)
                    {
                        var allowedTypes = new[] {
                    "image/jpeg", "image/jpg", "image/png", "image/gif",
                    "video/mp4", "video/avi", "video/mov"
                };

                        if (!allowedTypes.Contains(file.ContentType.ToLower()))
                        {
                            _logger.LogWarning($"File {file.FileName} có định dạng không được hỗ trợ: {file.ContentType}");
                            continue;
                        }

                        if (file.Length > 10 * 1024 * 1024)
                        {
                            _logger.LogWarning($"File {file.FileName} vượt quá kích thước cho phép (10MB)");
                            continue;
                        }

                        var stream = file.OpenReadStream();
                        var fileContent = new StreamContent(stream);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                        form.Add(fileContent, "PostFileLinks", file.FileName);

                        _logger.LogInformation($"Đã thêm file: {file.FileName} ({file.Length} bytes)");
                    }
                }
            }

            try
            {
                var response = await _httpClient.PostAsync($"{_apiSettings.BaseUrl}/Post", form);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Bài viết đã được tạo thành công.");
                    return RedirectToPage("/Index");
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Tạo bài viết thất bại - Mã lỗi: {StatusCode}, Nội dung: {Error}",
                        response.StatusCode, errorMessage);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gửi yêu cầu tạo bài viết.");
            }
            finally
            {
                // Đảm bảo dispose các stream
                form?.Dispose();
            }

            await OnGetAsync(currentPage);
            return Page();
        }

        //Update Post
        public async Task<IActionResult> OnPostUpdatePostAsync()
        {
            BaseUrl = _apiSettings.BaseUrl;

            try
            {
                // Lấy token từ cookie
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                // Giải mã token
                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jsonToken != null)
                {
                    CurrentUserId = jsonToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
                }

                // Tạo form data gửi lên API
                /* var form = new MultipartFormDataContent();
                 form.Add(new StringContent(NewPost.PostId.ToString()), "PostId");
                 form.Add(new StringContent(NewPost.Title ?? ""), "Title");
                 form.Add(new StringContent(NewPost.Content ?? ""), "Content");
                 form.Add(new StringContent(NewPost.CategoryId.ToString()), "CategoryId");
                 form.Add(new StringContent(CurrentUserId ?? ""), "UserId");*/

                var form = new MultipartFormDataContent
                {
                    { new StringContent(CurrentUserId), "UserId" },
                    { new StringContent(NewPost.Title), "Title" },
                    { new StringContent(NewPost.Content), "Content" },
                    { new StringContent(NewPost.CategoryId.ToString()), "CategoryId" },
                    { new StringContent(NewPost.PostId.ToString()), "PostId" }
                };

                if (NewPost.PostFileLinks != null)
                {
                    foreach (var file in NewPost.PostFileLinks)
                    {
                        var stream = file.OpenReadStream();
                        var fileContent = new StreamContent(stream);
                        fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
                        form.Add(fileContent, "PostFileLinks", file.FileName);
                    }
                }

                // Gửi PUT request
                var response = await _httpClient.PutAsync($"{BaseUrl}/Post", form);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Update post successfully!";
                    return RedirectToPage(); // hoặc gọi lại OnGetAsync nếu cần reload dữ liệu
                }
                else
                {
                    _logger.LogError("Update post failed. Status: " + response.StatusCode);
                    ModelState.AddModelError("", "Failed to update post.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating post.");
                ModelState.AddModelError("", "Unexpected error occurred.");
            }

            return Page();
        }

        //Delete Post
        public async Task<IActionResult> OnPostDeleteAsync(int postId)
        {
            try
            {
                // Kiểm tra Token
                if (!Request.Cookies.TryGetValue("AccessToken", out var token) || string.IsNullOrEmpty(token))
                    return RedirectToPage("/Login");

                // Lấy thông tin người dùng từ token
                var handler = new JwtSecurityTokenHandler();
                var jwtToken = handler.ReadToken(token) as JwtSecurityToken;
                if (jwtToken != null)
                {
                    CurrentUserId = jwtToken.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "nameid")?.Value;
                }

                // Có thể check role nếu cần (VD: chỉ cho phép xóa nếu là chính chủ bài viết)
                // if (!UserRoles.Contains("User")) return RedirectToPage("/Index");

                if (postId <= 0)
                {
                    ModelState.AddModelError("", "Invalid Post ID.");
                    return Page();
                }

                // Gọi API toggle delete
                var response = await _httpClient.PutAsync($"{_apiSettings.BaseUrl}/Post/toggle-deleted/{postId}", null);

                if (response.IsSuccessStatusCode)
                {
                    TempData["SuccessMessage"] = "Post deleted successfully.";
                    return RedirectToPage("/Index");
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Failed to delete post. Status: {Status}, Error: {Error}", response.StatusCode, error);
                    ModelState.AddModelError("", "Failed to delete post.");
                    TempData["ErrorMessage"] = "Failed to delete post.";
                    return Page();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while deleting post.");
                ModelState.AddModelError("", "Unexpected error occurred.");
                TempData["ErrorMessage"] = "Unexpected error occurred.";
                return Page();
            }
        }

    }
}


