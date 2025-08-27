namespace final_project_fe.Dtos.Question
{
    public class QuizImportRequest
    {
        public IFormFile PdfFile { get; set; }
        public int LessonId { get; set; }
        public int Number { get; set; }
        public string Difficulty { get; set; }
    }
}
