using Microsoft.AspNetCore.Identity;

namespace Presentation.Data.Entities;

public class UserEntity : IdentityUser
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string Password { get; set; } = null!;
    public string ConfirmPassword { get; set; } = null!;

}
