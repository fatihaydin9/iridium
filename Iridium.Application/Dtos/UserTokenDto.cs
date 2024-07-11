namespace Iridium.Application.Dtos;

public class UserTokenDto
{
    public string? AccessToken { get; set; }
    public DateTime ExpiresIn { get; set; }
}
