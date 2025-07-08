namespace final_project_fe.Dtos.Comment
{
    public class CommentPostDetailDto
    {
        public int CommentId { get; set; }
        public int PostId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Avatar { get; set; }
        public Guid UserId { get; set; }
        public int? ParentCommentId { get; set; }
        public string Content { get; set; }
    }
}
