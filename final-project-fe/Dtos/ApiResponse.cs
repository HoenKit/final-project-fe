using final_project_fe.Dtos.Users;
using System.Text.Json;

namespace final_project_fe.Dtos
{
	public class ApiResponse<T>
	{
		public T Result { get; set; }
	}

}
