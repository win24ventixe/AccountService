using Azure.Messaging.ServiceBus;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.IdentityModel.Tokens;
using Presentation.Data.Entities;
using Presentation.Data.Repositories;
using Presentation.Models;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Presentation.Services;

public interface IAccountService
{
    Task<UserResult> AddUserToRole(string userId, string roleName);
    Task<bool> AlreadyExistsAsync(string email);
    Task<UserResult> ConfirmEmailAsync(string email, string token);
    Task<LoginResponse?> LoginAsync(LogInRequest request);
    Task<UserResult> CreateUserAsync(CreateUserRequest request, string roleName = "Admin");
    Task<UserResult> DeleteUserAsync(string id);
    Task<UserResult> GetUsersAsync();
    Task<UserResult> UpdateUserAsync(UpdateUserRequest request);
    Task<UserResult> GetByIdAsync(string id);
}

public class AccountService(IUserRepository userRepository, UserManager<UserEntity> userManager, RoleManager<IdentityRole> roleManager, ServiceBusSender sender, IConfiguration configuration) : IAccountService
{
    private readonly UserManager<UserEntity> _userManager = userManager;
    private readonly RoleManager<IdentityRole> _roleManager = roleManager;
    private readonly ServiceBusSender _sender = sender;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IConfiguration _configuration = configuration;


    public async Task<UserResult> AddUserToRole(string userId, string roleName)
    {
        // Skapa rollen om den inte finns
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            var roleResult = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (!roleResult.Succeeded)
                return new UserResult { Success = false, Error = "Failed to create role." };
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return new UserResult { Success = false, Error = "User doesn't exist." };

        var result = await _userManager.AddToRoleAsync(user, roleName);
        return result.Succeeded
            ? new UserResult { Success = true }
            : new UserResult { Success = false, Error = "Unable to add user to role." };
    }
    public async Task<UserResult> ConfirmEmailAsync(string email, string token)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
            return new UserResult { Success = false, Error = "User not found." };

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? new UserResult { Success = true }
            : new UserResult { Success = false, Error = "Invalid or expired token." };
    }
    /* JWT */
    public async Task<LoginResponse?> LoginAsync(LogInRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return null;

        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!)
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // Get the secret key from configuration
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
            _configuration["Jwt:Key"] ?? "Jwt:Key"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddHours(1);

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"] ?? "Accounts",
            audience: _configuration["Jwt:Audience"] ?? "Accounts",
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );

        return new LoginResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Expiration = expires
        };
    }
 
    public async Task<UserResult> CreateUserAsync(CreateUserRequest request, string roleName = "Admin")
    {
        if (request == null)
            return new UserResult { Success = false, Error = "Form data can't be null." };

        var existsResult = await _userRepository.AlreadyExistAsync(x => x.Email == request.Email);
        if (existsResult.Success)
            return new UserResult { Success = false, Error = "User with same email already exists." };

        try
        {
            var userEntity = new UserEntity
            {
                FirstName = request.FirstName,
                LastName = request.LastName,
                Email = request.Email,
                UserName = request.Email,
                Password= request.Password,
                ConfirmPassword= request.ConfirmPassword
            };

            var addResult = await _userRepository.AddAsync(userEntity);

            if (addResult.Success)
            {
                var addToRoleResult = await AddUserToRole(userEntity.Id, roleName);

                // ✉️ Send verification email via Azure Service Bus
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(userEntity);
                var payload = new
                {
                    email = userEntity.Email,
                    token = Uri.EscapeDataString(token)
                };
                var json = JsonSerializer.Serialize(payload);
                await _sender.SendMessageAsync(new ServiceBusMessage(json));

                return addToRoleResult.Success
                    ? new UserResult { Success = true }
                    : new UserResult { Success = false, Error = "User created but not added to role." };
            }

            return new UserResult
            {
                Success = false,
                Error = "Failed to create user in repository."
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            return new UserResult { Success = false, Error = ex.Message };
        }
    }

    public async Task<UserResult> GetUsersAsync()
    {
        var result = await _userRepository.GetAllAsync();

        if (!result.Success)
        {
            return new UserResult
            {
                Success = false,
                Error = result.Error
            };
        }

        var users = result.Result?.Select(userEntity => new User
        {
            Id = userEntity.Id,
            Email = userEntity.Email!,
            Role = _userManager.GetRolesAsync(userEntity).Result.FirstOrDefault() ?? "User",
            Token = _userManager.GenerateEmailConfirmationTokenAsync(userEntity).Result
        }).ToList() ?? new List<User>();

        return new UserResult
        {
            Success = true,
        };
    }
    public async Task<UserResult> GetByIdAsync(string id)
    {
        var userEntityResult = await _userRepository.GetAsync(user => user.Id == id);
        if (!userEntityResult.Success || userEntityResult.Result == null)
            return new UserResult { Success = false, Error = "User not found." };

        var userEntity = userEntityResult.Result;

        var roles = await _userManager.GetRolesAsync(userEntity);
        var user = new User
        {
            Id = userEntity.Id,
            Email = userEntity.Email!,
            Role = roles.FirstOrDefault() ?? "User",
            Token = await _userManager.GenerateEmailConfirmationTokenAsync(userEntity)
        };
        return new UserResult { Success = true };
    }

    public async Task<bool> AlreadyExistsAsync(string email)
    {
        var result = await _userRepository.AlreadyExistAsync(x => x.Email == email);
        return result.Success;
    }
    public async Task<UserResult> UpdateUserAsync(UpdateUserRequest request)
    {
        if (request == null)
            return new UserResult { Success = false, Error = "Not all required fields are supplied." };

        var userEntity = new UserEntity
        {
            Id = request.Id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            UserName = request.Email
        };
        var result = await _userRepository.UpdateAsync(userEntity);

        return result.Success
            ? new UserResult { Success = true }
            : new UserResult { Success = false, Error = result.Error };
    }

    public async Task<UserResult> DeleteUserAsync(string id)
    {
        var userEntity = new UserEntity { Id = id };
        var result = await _userRepository.DeleteAsync(userEntity);

        return result.Success
            ? new UserResult { Success = true }
            : new UserResult { Success = false, Error = result.Error };
    }
}  

