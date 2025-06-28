namespace final_project_fe.Dtos.Courses
{
    public class UserCourseDto
    {
        public int CourseId { get; set; }
        public string? CourseName { get; set; }
        public string? CourseImage { get; set; }
        public string? CertificateLink { get; set; }
        public string? Status { get; set; }
        public float? Percentage { get; set; }
        public DateTime? CompletedAt { get; set; }
    }


}
