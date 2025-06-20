namespace final_project_fe.Dtos.Mentors
{
	public class MentorDto
	{
		public string FirstName { get; set; }
		public string LastName { get; set; }
	}
	public class GetMentorDto
	{
		public int MentorId { get; set; }
		public Guid UserId { get; set; }
		public string? StudyLevel { get; set; }
		public string? CitizenID { get; set; }
		public string? Degree { get; set; }
		public string? Signature { get; set; }
		public string? IssuePlace { get; set; }
		public DateTime ExpiredDate { get; set; }
		public DateTime IssueDate { get; set; }
		public DateTime CreateAt { get; set; }
		public DateTime UpdateAt { get; set; }
	}
}
