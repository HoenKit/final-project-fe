namespace final_project_fe.Dtos.Users
{
    public class LoginDto
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }
    public class LoginResponseDto
    {
        public string Token { get; set; }
    }
    public class GoogleLoginResponse
    {
        public string Url { get; set; }
        public string? Message { get; set; }
        public bool Success { get; set; }
    }
}
