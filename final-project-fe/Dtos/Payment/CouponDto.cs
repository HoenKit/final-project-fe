using final_project_fe.Dtos.Courses;

namespace final_project_fe.Dtos.Payment
{
    public class CouponDto
    {
            public int CouponId { get; set; }
            public string CouponName { get; set; } = string.Empty;
            public float Discount { get; set; }
    }
    public class BuyCourseRequest
    {
        public string UserId { get; set; }
        public int CourseId { get; set; }
        public int CouponId { get; set; }
    }
    public class AddCouponDto
    {
        public int CouponId { get; set; }
        public List<int> CourseIds { get; set; } = new();
        public DateTime ExpiredAt { get; set; }
    }


    public class CourseResponse
    {
        public List<CourseDto> Items { get; set; } = new();
    }
}
