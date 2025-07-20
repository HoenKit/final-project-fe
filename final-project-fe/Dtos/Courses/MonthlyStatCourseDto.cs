namespace final_project_fe.Dtos.Courses
{
    public class MonthlyStatCourseDto
    {
        public string Time { get; set; }
        public int TotalCoursesCreated { get; set; }
        public int TotalStudentsEnrolled { get; set; }
        public decimal TotalEarnings { get; set; }
    }
}
