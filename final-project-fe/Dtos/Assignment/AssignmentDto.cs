namespace final_project_fe.Dtos.Assignment
{
    public class AssignmentDto
    {
        public int LessonId { get; set; }
        public string Content { get; set; }
        public string meetLink { get; set; }
    }
    public class AssignmentResponseDto
    {
        public int AssignmentId { get; set; }
        public int LessonId { get; set; }
        public string Content { get; set; }
        public string? MeetLink { get; set; }
        public DateTime CreateAt { get; set; }
    }
}
