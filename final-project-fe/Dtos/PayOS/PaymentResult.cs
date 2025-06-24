namespace final_project_fe.Dtos.PayOS
{
    public class PaymentResult
    {
        public bool Success { get; set; }
        public string PaymentUrl { get; set; }
        public long OrderCode { get; set; }
        public string ErrorMessage { get; set; }
    }
}
