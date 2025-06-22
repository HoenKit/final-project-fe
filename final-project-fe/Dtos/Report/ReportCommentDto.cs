namespace final_project_fe.Dtos.Report
{
    public class ReportCommentDto
    {
        public int CommentId { get; set; }
        public int ReportId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
    }
}
