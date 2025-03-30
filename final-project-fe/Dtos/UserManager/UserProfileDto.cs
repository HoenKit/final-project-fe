namespace final_project_fe.Dtos.UserManager
{
    public class UserProfileDto
    {
        public Guid UserId { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public DateTime? Birthday { get; set; }
        public string Gender { get; set; }
        public string Address { get; set; }
    }
}
