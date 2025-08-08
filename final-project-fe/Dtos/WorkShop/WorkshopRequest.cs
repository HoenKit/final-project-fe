namespace final_project_fe.Dtos.WorkShop
{
    public class WorkshopRequest
    {
        public int MentorId { get; set; }
        public string Decription { get; set; } = string.Empty; 
        public string StreamingLink { get; set; } = string.Empty;

    }
    public class WorkShopDto
    {
        public int WorkShopId { get; set; }
        public int MentorId { get; set; }
        public string Decription { get; set; }
        public string StreamingLink { get; set; }
        public DateTime CreateAt { get; set; }
        public DateTime UpdateAt { get; set; }
    }
}
