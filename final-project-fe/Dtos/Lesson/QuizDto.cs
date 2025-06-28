namespace final_project_fe.Dtos.Lesson
{
    public class QuizDto
    {
        public class QuestionDto
        {
            public string QuestionText { get; set; }
            public string QuestionType { get; set; }
            public List<AnswerDto> Answers { get; set; }
        }

        public class AnswerDto
        {
            public string AnswerId { get; set; }
            public string Text { get; set; }
            public bool IsCorrect { get; set; }
        }
    }
}
