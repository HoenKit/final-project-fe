namespace final_project_fe.Dtos.Lesson
{
    public class LessonDto
    {
        public int LessonId { get; set; }
        public int ModuleId { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public IFormFile? Document { get; set; }
        public IFormFile? Video { get; set; }
    }

    public class LessonDetailDto
    {
        public string Title { get; set; }
        public string Description { get; set; }
        public string VideoLink { get; set; }
        public string DocumentLink { get; set; }
    }

    public class LessonSubmitDto
    {
        public Guid UserId { get; set; }
        public int LessonId { get; set; }
        public List<int> AnswerIds { get; set; }
    }
}
