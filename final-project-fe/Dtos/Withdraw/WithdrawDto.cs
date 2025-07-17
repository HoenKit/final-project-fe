namespace final_project_fe.Dtos.Withdraw
{
    public class WithdrawDto
    {
        public int WithdrawId { get; set; }
        public int MentorId { get; set; }
        public decimal Points { get; set; }
        public decimal Amount { get; set; }
        public DateTime CreateAt { get; set; }
        public string Status { get; set; }
    }
}
