using final_project_fe.Dtos.Users;

namespace final_project_fe.Dtos.Comment
{
    public class CommentDto
    {
        public int CommentId { get; set; }
        public int PostId { get; set; }
        public Guid UserId { get; set; }
        public int? ParentCommentId { get; set; }
        public string Content { get; set; }
		public DateTime CreateAt { get; set; } 
		public DateTime? UpdateAt { get; set; }
		public User? User { get; set; }
	}
}
