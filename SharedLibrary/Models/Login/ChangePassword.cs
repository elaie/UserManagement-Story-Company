using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharedLibrary.Models.Authentication.Login
{
    public class ChangePassword
    {
        public string? Email { get; set; }
        public required string OldPassword { get; set; }
        public required string NewPassword { get; set; }
        public required string ConfirmPassword { get; set; }

    }
}
