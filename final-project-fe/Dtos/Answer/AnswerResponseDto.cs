namespace final_project_fe.Dtos.Answer
{
    public class AnswerResponseDto
    {
        public int AnswerId { get; set; }
        public int QuestionId { get; set; }
        public string Text { get; set; }
        public bool Is_correct { get; set; }
    }
}
