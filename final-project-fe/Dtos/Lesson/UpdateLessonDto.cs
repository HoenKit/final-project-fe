namespace final_project_fe.Dtos.Lesson
{
    public class UpdateLessonDto
    {
        public int LessonId { get; set; }
        public int ModuleId { get; set; }
        public string Title { get; set; }
        public string? Description { get; set; }
        public IFormFile? Video { get; set; }
    }
}
