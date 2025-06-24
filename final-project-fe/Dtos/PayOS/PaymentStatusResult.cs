namespace final_project_fe.Dtos.PayOS
{
    public class PaymentStatusResult
    {
        public bool Success { get; set; }
        public string Status { get; set; }
        public int Amount { get; set; }
        public long OrderCode { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsPaid => Success && Status == "PAID";
        public int Points => Amount / 1000;
    }
}
