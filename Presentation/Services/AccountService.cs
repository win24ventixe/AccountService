using Microsoft.AspNetCore.Identity;

namespace Presentation.Services;
public interface IAccountService
{
}
public class AccountService(UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager) : IAccountService
{
    private readonly UserManager<IdentityUser> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;

  
}

