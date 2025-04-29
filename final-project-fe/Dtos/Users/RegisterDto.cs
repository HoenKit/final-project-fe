using System.ComponentModel.DataAnnotations;

namespace final_project_fe.Dtos.Users
{
    public class RegisterDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; }
        [Required]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters.")]
        [RegularExpression(@"^(?=.*[0-9])(?=.*[!@#$%^&*()_+\-=\[\]{};':""\\|,.<>\/?]).{6,}$",
        ErrorMessage = "Password must contain at least 1 number and 1 special character.")]
        public string Password { get; set; }
        [Required]
        [Compare("Password", ErrorMessage = "Confirmation password does not match.")]
        public string ConfirmPassword { get; set; }
        [Required]
        [Phone]
        public string Phone { get; set; }
        [Required]
        public UserMetadataDto UserMetadataDto { get; set; }
    }
    public class UserMetadataDto
    {
        [Required]
        public string FirstName { get; set; }
        [Required]
        public string LastName { get; set; }
        [Required]
        public DateTime Birthday { get; set; }
        [Required]
        public string Gender { get; set; }
        [Required]
        public string Address { get; set; }
    }
}
