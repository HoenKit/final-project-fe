using System.ComponentModel.DataAnnotations.Schema;

namespace final_project_fe.Dtos.Users
{
	public class UserMetadata
	{
		public int UserMetadataId { get; set; }
		public Guid UserId { get; set; }
		public string FirstName { get; set; }
		public string LastName { get; set; }
		public DateTime? Birthday { get; set; }
		public string? Gender { get; set; }
		public string? Avatar { get; set; }
		public string? Address { get; set; }
        public string? Nationality { get; set; }
        public string? Level { get; set; }
        public string? Goals { get; set; }
        public string? FavouriteSubject { get; set; }
    }
    public class UpdateAvatarDto
    {
        public IFormFile? Avatar { get; set; }
    }
}
