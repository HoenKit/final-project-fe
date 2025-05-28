namespace final_project_fe.Dtos.Report
{
    public class ReportPostDto
    {
        public int ReportId { get; set; }
        public int PostId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
    }
}
