using final_project_fe.Dtos.Answer;

namespace final_project_fe.Dtos.Question
{
    public class QuestionResponseDto
    {
        public int QuestionId { get; set; }
        public int LessonId { get; set; }
        public string Question_text { get; set; }
        public string QuestionType { get; set; }
        public List<AnswerResponseDto> Answers { get; set; }
    }
}
