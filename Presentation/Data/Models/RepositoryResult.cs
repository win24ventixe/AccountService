namespace Presentation.Data.Models;

public class RepositoryResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}

public class RepositoryResult<T> : RepositoryResult
{
    public T? Result { get; set; }
}
