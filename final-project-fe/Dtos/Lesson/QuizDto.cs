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
        public class LessonContentResponseDto
        {
            public List<QuestionDto> QuizQuestions { get; set; }
            public LessonDetailDto LessonDetail { get; set; }
        }

        public class UserAssignmentDto
        {
            public int AssignmentId { get; set; }  
            public string UserId { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public bool IsScored { get; set; }
            public bool IsPresented { get; set; }
        }
        public class AnswerDto
        {
            public string AnswerId { get; set; }
            public string Text { get; set; }
            public bool IsCorrect { get; set; }
        }
    }
}
