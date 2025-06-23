namespace final_project_fe.Dtos.Reviews
{
    public class CourseReviewPageResult
    {
        public List<ReviewResponseDto> Reviews { get; set; }
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public decimal AverageRating { get; set; }
        public int ReviewCount { get; set; }

        public CourseReviewPageResult(List<ReviewResponseDto> reviews, int totalCount, int page, int pageSize, decimal averageRating, int reviewCount)
        {
            Reviews = reviews;
            TotalCount = totalCount;
            Page = page;
            PageSize = pageSize;
            AverageRating = averageRating;
            ReviewCount = reviewCount;
        }
    }
}
