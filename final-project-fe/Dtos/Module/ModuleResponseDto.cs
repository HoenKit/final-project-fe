namespace final_project_fe.Dtos.Module
{
    public class ModuleResponseDto
    {
        public int ModuleId { get; set; }
        public int CourseId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public bool IsPremium { get; set; }
        public int CountLesson { get; set; }
    }
}
