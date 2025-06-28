using final_project_fe.Dtos.Lesson;

namespace final_project_fe.Dtos.Module
{
    public class ModuleDto
    {
        public int ModuleId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsPremium { get; set; }
    }
    public class ModuleWithLessonDto
    {
        public int ModuleId { get; set; }
        public int CourseId { get; set; } 
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsPremium { get; set; }
        public int CountLesson { get; set; }
        public List<LessonbyModuleDto> Lessons { get; set; }
    }
    public class ModuleProgressDto
    {
        public Guid UserId { get; set; }
        public int ModuleId { get; set; }
        public string Title { get; set; }
        public float Percentage { get; set; }
        public List<LessonbyModuleDto> Lessons { get; set; }
    }


}
