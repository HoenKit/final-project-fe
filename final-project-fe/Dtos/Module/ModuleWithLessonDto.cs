using final_project_fe.Dtos.Lesson;

namespace final_project_fe.Dtos.Module
{
    public class ModuleWithLessonsDto
    {
        public int ModuleId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsPremium { get; set; }
        public int CountLesson { get; set; }
        public List<LessonResponseDto> Lessons { get; set; } = new();
    }
}
