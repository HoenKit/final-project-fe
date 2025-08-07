namespace final_project_fe.Dtos.Users
{
    public class UpdateUserMetadataDto
    {
        public Guid userId { get; set; }
        public string? Level { get; set; }
        public string? Goals { get; set; }
        public string? FavouriteSubject { get; set; }
    }
}
