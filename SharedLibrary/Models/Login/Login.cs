using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Models.Login
{
    public class Login
    {
        [Required(ErrorMessage = "User name is required")]
        [DefaultValue("elaiextha@gmail.com")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "User name is required")]
        [DefaultValue("Test@123")]
        public string? Password { get; set; }
    }
}
