namespace final_project_fe.Dtos.Mentors
{
	public class MentorDto
	{
        public Guid UserId { get; set; }
        public string FirstName { get; set; }
		public string LastName { get; set; }
	}
    public class MentorResponse
    {
        public int MentorId { get; set; }
    }
    public class MentorInforDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Signature { get; set; }
        public string JobTile { get; set; }
    }
    public class GetMentorDto
	{
		public int MentorId { get; set; }
		public Guid UserId { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string? Introduction { get; set; }
        public string? JobTitle { get; set; }
        public string? StudyLevel { get; set; }
		public string? CitizenID { get; set; }
		public string? Degree { get; set; }
		public string? Signature { get; set; }
		public string? IssuePlace { get; set; }
		public DateTime ExpiredDate { get; set; }
		public DateTime IssueDate { get; set; }
		public DateTime CreateAt { get; set; }
		public DateTime UpdateAt { get; set; }
        public int TotalCourses { get; set; } = 0;
        public int TotalStudents { get; set; } = 0;
        public int TotalReviews { get; set; } = 0;
        public decimal AverageRating { get; set; } = 0;
        public string? AccountNumber { get; set; }
        public string? AccountName { get; set; }
        public string? AccountBank { get; set; }
    }
    public class CreateMentorDto
    {
        public Guid UserId { get; set; }
        public string? Introduction { get; set; }
        public string? JobTitle { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? StudyLevel { get; set; }
        public string? CitizenID { get; set; }
        public IFormFile? Signature { get; set; }
        public string? IssuePlace { get; set; }
        public DateTime? ExpiredDate { get; set; }
        public DateTime? IssueDate { get; set; }
        public DateTime CreateAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdateAt { get; set; } = DateTime.UtcNow;
        public List<MentorCertificateDto>? MentorCertificates { get; set; }
    }
    public class CtzResultDto
    {
        public string? CitizenId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public DateTime? IssueDate { get; set; }
    }
    public class MentorCertificateDto
    {
        public int MentorCertificateId { get; set; }
        public int MentorId { get; set; }
        public string? FileUrl { get; set; }
        public string? CertificateName { get; set; }
    }

}
