namespace final_project_fe.Dtos.Payment
{
    public class GetPaymentDto
    {
        public int PaymentId { get; set; }
        public Guid UserId { get; set; }
        public string Email { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public string ServiceType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
