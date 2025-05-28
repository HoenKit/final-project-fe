namespace final_project_fe.Dtos.Users
{
	public class User
	{
		public Guid UserId { get; set; }
		public string Email { get; set; }
		public string Phone { get; set; }
		public string Password { get; set; }
		public decimal Point { get; set; }
		public bool IsBanned { get; set; } = false;
		public DateTime CreateAt { get; set; } 
		public DateTime? UpdateAt { get; set; } 
		public UserMetadata? UserMetaData { get; set; }
		public int TotalCount { get; set; }

    }
}
