namespace final_project_fe.Dtos.Courses
{
	public class UpdateCourseDto
	{
		public int CourseId { get; set; }
		public int MentorId { get; set; }
		public int CategoryId { get; set; }
		public string CourseName { get; set; }
		public string CourseContent { get; set; }
		public decimal Cost { get; set; }
		public string SkillLearn { get; set; }
		public IFormFile? CoursesImage { get; set; }
		public double? CourseLength { get; set; }
	}
}
