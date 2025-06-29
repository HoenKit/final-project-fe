using final_project_fe.Dtos.Answer;

namespace final_project_fe.Dtos.Question
{
    public class QuizQuestion
    {
        public int LessonId { get; set; }
        public string QuestionText { get; set; }
        public string QuestionType { get; set; }
        public List<QuizAnswer> Answers { get; set; }
    }
}
