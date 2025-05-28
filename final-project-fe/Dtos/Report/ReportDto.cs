namespace final_project_fe.Dtos.Report
{
    public class ReportDto
    {
        public int ReportId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
    }
}
