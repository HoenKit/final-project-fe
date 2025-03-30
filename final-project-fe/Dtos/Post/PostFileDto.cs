namespace final_project_fe.Dtos.Post
{
	public class PostFileDto
	{
		public int PostFileId { get; set; }
		public int PostId { get; set; }
		public string FileUrl { get; set; }
		public string PostFileType { get; set; }
		public bool? IsDeleted { get; set; }
	}
}
