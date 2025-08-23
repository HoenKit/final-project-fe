namespace final_project_fe.Dtos.Payment
{
    public class GetPaymentDto
    {
        public int PaymentId { get; set; }
        public Guid UserId { get; set; }
        public string Email { get; set; }
		public int? CourseId { get; set; }
		public String? CourseName { get; set; }
        public String? PlanName { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public string ServiceType { get; set; }
        public DateTime CreatedAt { get; set; }
    }
    public class PremiumPackage
    {
        public int PlanId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public object PaymentPlans { get; set; }
    }
}
