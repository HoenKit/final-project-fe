namespace final_project_fe.Dtos.Report
{
    public class GroupedReportDto<TId, T>
    {
        public TId Id { get; set; }
        public int ReportCount { get; set; }
        public List<T> Reports { get; set; }
    }
}
