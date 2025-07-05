using final_project_fe.Dtos.Question;

namespace final_project_fe.Dtos.Lesson
{
    public class LessonResponseDto
    {
        public int LessonId { get; set; }
        public int ModuleId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? DocumentLink { get; set; }
        public string? VideoLink { get; set; }
        public List<QuestionResponseDto> Questions { get; set; } = new();

    }
    public class LessonbyModuleDto
    {
        public int LessonId { get; set; }
        public int ModuleId { get; set; }
        public bool Ispassed { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
    public class LessonCompletionDto
    {
        public Guid UserId { get; set; }
        public int LessonId { get; set; }
        public DateTime CompletedAt { get; set; }
        public float? Mark { get; set; }
        public bool IsPassed { get; set; }
    }
}
