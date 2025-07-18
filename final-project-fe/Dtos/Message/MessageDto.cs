namespace final_project_fe.Dtos.NewFolder
{
    public class MessageDto
    {
        public int MessageId { get; set; }                
        public Guid SenderId { get; set; }        
        public Guid ReceiverId { get; set; }         
        public string Content { get; set; }     
        public DateTime SentAt { get; set; }
    }
}
