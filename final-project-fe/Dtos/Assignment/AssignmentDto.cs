namespace final_project_fe.Dtos.Assignment
{
    public class AssignmentDto
    {
        public int AssignmentId { get; set; }
        public int LessonId { get; set; }
        public string Content { get; set; }
        public TimeSpan? ExamTime { get; set; }
        public string? MeetLink { get; set; }
    }
    public class AssignmentResponseDto
    {
        public int AssignmentId { get; set; }
        public int LessonId { get; set; }
        public string Content { get; set; }
        public string? MeetLink { get; set; }
        public DateTime CreateAt { get; set; }
        public TimeSpan? ExamTime { get; set; }
    }

    public class CourseWithAssignmentsDto
    {
        public int CourseId { get; set; }
        public string CourseTitle { get; set; }
        public int AssignmentCount { get; set; }
    }
    public class GetAssignmentbycreatorDto
    {
        public int AssignmentId { get; set; }
        public int LessonId { get; set; }
        public string Content { get; set; }
        public string? MeetLink { get; set; }
        public string? Title { get; set; }
    }
}
