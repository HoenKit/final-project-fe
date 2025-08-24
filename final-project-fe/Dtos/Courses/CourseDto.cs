using final_project_fe.Dtos.Assignment;

namespace final_project_fe.Dtos.Courses
{
	public class CourseDto
	{
        public int MentorId { get; set; }
        public int CourseId { get; set; }
        public int CategoryId { get; set; }
        public string? CourseName { get; set; }
        public string? CourseContent { get; set; }
        public decimal Cost { get; set; } = 0;
        public string? SkillLearn { get; set; }
        public IFormFile? CoursesImage { get; set; }
        public double? CourseLength { get; set; }
    }
    public class ListCourseDto 
    {

        public int CourseId { get; set; }

        public string? CourseName { get; set; }

        public int MentorId { get; set; }
        public AssignmentDto? Assignment { get; set; }
    }
    public class CourseCertificateDto
    {
        public string courseName { get; set; }
    }
}
