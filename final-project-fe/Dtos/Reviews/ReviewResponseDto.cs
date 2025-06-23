using final_project_fe.Dtos.Users;

namespace final_project_fe.Dtos.Reviews
{
    public class ReviewResponseDto
    {
        public int ReviewId { get; set; }
        public int CourseId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
        public bool IsDeleted { get; set; } = false;
        public decimal rate { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }
        public User? User { get; set; }
    }
}
