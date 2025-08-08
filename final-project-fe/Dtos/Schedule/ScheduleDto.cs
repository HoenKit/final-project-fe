namespace final_project_fe.Dtos.Schedule
{
    public class ScheduleDto
    {
        public int ScheduleId { get; set; }
        public int MentorId { get; set; }
        public string ScheduleName { get; set; } = string.Empty;
        public DateTime MentorDay { get; set; }
        public DateTime CreateAt { get; set; }
        public int CourseId { get; set; }
    }

    public class UserScheduleDto
    {
        public Guid UserId { get; set; }
        public int ScheduleId { get; set; }
    }
}
