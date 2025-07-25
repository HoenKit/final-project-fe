﻿namespace final_project_fe.Dtos.Courses
{
	public class UpdateCourseDto
	{
        public int CourseId { get; set; }
        public int MentorId { get; set; }
        public int CategoryId { get; set; }
        public string? CourseName { get; set; }
        public string? CourseContent { get; set; }
        public string? Requirement { get; set; }
        public string? IntendedLearner { get; set; }
        public string? Language { get; set; }
        public string? Level { get; set; }
        public decimal Cost { get; set; } = 0;
        public string? SkillLearn { get; set; }
        public int? StudentCount { get; set; }
        public IFormFile? CoursesImage { get; set; }
        public double? CourseLength { get; set; }
    }
}
