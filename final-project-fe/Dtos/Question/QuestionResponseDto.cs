namespace final_project_fe.Dtos.Question
{
    public class QuestionResponseDto
    {
        public int QuestionId { get; set; }
        public int LessonId { get; set; }
        public string Question_text { get; set; }
        public string QuestionType { get; set; }
    }
}
