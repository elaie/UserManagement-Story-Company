using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Models.User
{
    public class UserDto
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
     
        public UserDto(string id, string userName, string email, string phoneNumber )
        {
            Id = id;
            UserName = userName;
            Email = email;
            PhoneNumber = phoneNumber;
        }
        public UserDto() { }
    }
    public class UpdateUserDto
    {
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }

}
