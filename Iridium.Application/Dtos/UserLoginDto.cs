using System.ComponentModel.DataAnnotations;

namespace Iridium.Application.Dtos;

public class UserLoginDto
{
    [Required] public string MailAddress { get; set; }

    [Required] public string Password { get; set; }
}
