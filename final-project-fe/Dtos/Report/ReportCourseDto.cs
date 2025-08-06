namespace final_project_fe.Dtos.Report
{
    public class ReportCourseDto
    {
        public int ReportId { get; set; }
        public int CourseId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
    }
}
