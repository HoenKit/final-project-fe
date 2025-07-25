﻿using System.Text.Json.Serialization;

namespace final_project_fe.Dtos.Users
{
	public class User
	{
		public Guid UserId { get; set; }
		public string Email { get; set; }
		public string Phone { get; set; }
		public string Password { get; set; }
		public decimal? Point { get; set; }
		public bool IsBanned { get; set; } 
        public bool IsPremium { get; set; }
        public DateTime CreateAt { get; set; } 
		public DateTime? UpdateAt { get; set; } 
		public UserMetadata? UserMetaData { get; set; }
		public int TotalCount { get; set; }

    }
    public class UserDto
    {
        [JsonPropertyName("userId")]
        public string UserId { get; set; }

        [JsonPropertyName("email")]
        public string Email { get; set; }
        [JsonPropertyName("isPremium")]
        public bool IsPremium { get; set; }

        [JsonPropertyName("userMetaData")]
        public UserMetaDataDto UserMetaData { get; set; }
    }
    public class UserMetaDataDto
    {
        [JsonPropertyName("firstName")]
        public string FirstName { get; set; }

        [JsonPropertyName("lastName")]
        public string LastName { get; set; }

        // các trường khác nếu cần
    }
}
