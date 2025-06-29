using final_project_fe.Dtos.Users;

namespace final_project_fe.Dtos.Transaction
{
    public class GetTransactionDto
    {
        public int TransactionId { get; set; }
        public Guid UserId { get; set; }
        public decimal? Points { get; set; }
        public string? PaymentMethod { get; set; }
        public string? Status { get; set; }
        public decimal? Amount { get; set; }
        public string? OrderCode { get; set; }
        public DateTime CreateAt { get; set; }
        public User? User { get; set; }
    }
}
