namespace final_project_fe.Dtos.Lesson
{
    public class LessonDto
    {
        public int ModuleId { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public IFormFile? Video { get; set; }
    }
}
