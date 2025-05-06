using final_project_fe.Dtos.Comment;
using final_project_fe.Dtos.Users;

namespace final_project_fe.Dtos.Post
{
    public class PostCreateDto
    {
        public int PostId { get; set; }
        public Guid UserId { get; set; }
        public int? ParentPostId { get; set; }
        public int SubCategoryId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public List<PostFileDto>? PostFiles { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime? UpdateAt { get; set; }
        public User? User { get; set; }
    }
}
