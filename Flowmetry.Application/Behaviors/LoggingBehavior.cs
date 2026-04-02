using MediatR;
using Microsoft.Extensions.Logging;

namespace Flowmetry.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        logger.LogInformation("Handling {RequestType}", typeof(TRequest).Name);
        try
        {
            var response = await next();
            logger.LogInformation("Handled {RequestType} successfully", typeof(TRequest).Name);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling {RequestType}", typeof(TRequest).Name);
            throw;
        }
    }
}
