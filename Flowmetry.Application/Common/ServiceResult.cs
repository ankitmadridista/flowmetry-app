namespace Flowmetry.Application.Common;

public abstract record ServiceResult
{
    public record Success : ServiceResult;
    public record Failure(string Reason) : ServiceResult;
}
