namespace Presentation.Models;

public class UserResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}
public class UserResult<T> : UserResult
{
    public T? Result { get; set; }
}