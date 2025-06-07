using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Presentation.Models;
using Presentation.Services;

namespace Presentation.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AccountsController(IAccountService accountService) : ControllerBase
{
    private readonly IAccountService _accountService = accountService;

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmEmail(ConfirmEmailRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest("Invalid data.");

        var result = await _accountService.ConfirmEmailAsync(request.Email, request.Token);

        return result.Success ? Ok() : BadRequest(result.Error);
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateUserAsync(CreateUserRequest request, string roleName = "Admin")
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);
        var result = await _accountService.CreateUserAsync(request, roleName);
        return result.Success ? Ok() : BadRequest(result.Error);
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsersAsync()
    {
        var result = await _accountService.GetUsersAsync();
        return result.Success ? Ok() : BadRequest(result.Error);
    }

    [HttpGet("user/{id}")]
    public async Task<IActionResult> GetUserByIdAsync(string id)
    {
        var result = await _accountService.GetByIdAsync(id);
        return result.Success ? Ok() : BadRequest(result.Error);
    }

    [HttpGet("exists/{email}")]
    public async Task<IActionResult> AlreadyExists(string email)
    {
        var exists = await _accountService.AlreadyExistsAsync(email);
        return Ok(new { exists = exists });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LogInRequest request)
    {
        var result = await _accountService.LoginAsync(request);
        if (result == null)
            return Unauthorized("Invalid email or password.");

        return Ok(result); // result should be a LoginResponse
    }
}
