namespace final_project_fe.Dtos.Reviews
{
    public class UpdateReviewDto
    {
        public int ReviewId { get; set; }
        public int CourseId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
        public bool IsDeleted { get; set; } = false;
        public decimal Rate { get; set; }
    }
}
