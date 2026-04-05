namespace Flowmetry.Application.Common;

public abstract record Result<T>
{
    public record Success(T Value) : Result<T>;
    public record NotFound(string Message) : Result<T>;
    public record ValidationFailure(IEnumerable<string> Errors) : Result<T>;
    public record Conflict(string Message) : Result<T>;
}
