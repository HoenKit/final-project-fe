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
        [DataType(DataType.Date)]
        [CustomBirthdayValidation(18, 1900, ErrorMessage = "Birthday must be after 1900 and you must be at least 18 years old.")]
        public DateTime Birthday { get; set; }
        [Required]
        public string Gender { get; set; }
        [Required]
        public string Address { get; set; }
    }
    public class CustomBirthdayValidation : ValidationAttribute
    {
        private readonly int _minAge;
        private readonly int _minYear;

        public CustomBirthdayValidation(int minAge, int minYear)
        {
            _minAge = minAge;
            _minYear = minYear;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is DateTime date)
            {
                int age = DateTime.Now.Year - date.Year;
                if (date > DateTime.Now.AddYears(-age)) age--; // chỉnh nếu chưa sinh nhật trong năm

                if (age < _minAge || date.Year < _minYear)
                    return new ValidationResult(ErrorMessage);
            }
            return ValidationResult.Success;
        }
    }
}
