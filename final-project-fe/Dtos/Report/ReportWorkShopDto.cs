namespace final_project_fe.Dtos.Report
{
    public class ReportWorkShopDto
    {
        public int ReportId { get; set; }
        public int WorkShopId { get; set; }
        public Guid UserId { get; set; }
        public string Content { get; set; }
    }
}
