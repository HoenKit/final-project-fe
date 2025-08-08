using final_project_fe.Dtos.Comment;
using final_project_fe.Dtos.Users;

namespace final_project_fe.Dtos.Post
{
    public class PostCreateDto
    {
        public int PostId { get; set; }
        public Guid UserId { get; set; }
        public int? ParentPostId { get; set; }
        public int CategoryId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public List<IFormFile>? PostFileLinks { get; set; }

    }
}
