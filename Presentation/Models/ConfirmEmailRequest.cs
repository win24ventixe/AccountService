namespace Presentation.Models;

public class ConfirmEmailRequest {
    public string Email { get; set; } = null!;
    public string Token { get; set; } = null!;
}