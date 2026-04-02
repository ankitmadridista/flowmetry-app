using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Flowmetry.Application.Behaviors;

public class PerformanceBehavior<TRequest, TResponse>(ILogger<PerformanceBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var response = await next();
        sw.Stop();

        if (sw.ElapsedMilliseconds > 500)
            logger.LogWarning("Long running request: {RequestType} took {ElapsedMs}ms", typeof(TRequest).Name, sw.ElapsedMilliseconds);

        return response;
    }
}
