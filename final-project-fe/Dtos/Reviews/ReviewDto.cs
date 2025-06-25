namespace final_project_fe.Dtos.Reviews
{
    public class ReviewDto
    {
        public int CourseId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
        public bool IsDeleted { get; set; } = false;
        public decimal Rate { get; set; }
    }
}
