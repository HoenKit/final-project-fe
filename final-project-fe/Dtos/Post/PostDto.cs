using final_project_fe.Dtos.Comment;

namespace final_project_fe.Dtos.Post
{
    public class PostDto
    {
        public int PostId { get; set; }
        public Guid UserId { get; set; }
        public int? ParentPostId { get; set; }
        public int SubCategoryId { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
    }
}
