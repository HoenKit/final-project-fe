using final_project_fe.Dtos.Mentors;

namespace final_project_fe.Dtos.Courses
{
	public class GetCourseDto
	{
        public int CourseId { get; set; }
        public string CourseName { get; set; }
        public string? CourseContent { get; set; }
        public decimal Cost { get; set; } = 0;
        public string? SkillLearn { get; set; }
        public string? Requirement { get; set; }
        public string? IntendedLearner { get; set; }
        public string? Language { get; set; }
        public string? Level { get; set; }
        public int? StudentCount { get; set; }
        public string? CoursesImage { get; set; }
        public double? CourseLength { get; set; }
        public string? Status { get; set; }
        public Decimal? AverageRating { get; set; }
        public decimal? TotalReviews { get; set; }
        public DateTime? CreateAt { get; set; }
        public MentorDto? Mentor { get; set; }
    }
}
