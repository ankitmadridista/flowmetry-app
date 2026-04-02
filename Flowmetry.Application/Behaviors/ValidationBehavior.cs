using FluentValidation;
using MediatR;

namespace Flowmetry.Application.Behaviors;

public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(f => f != null)
            .ToList();

        if (failures.Count == 0) return await next();

        var errors = failures.Select(f => f.ErrorMessage);
        var resultType = typeof(TResponse);
        var failureType = resultType.GetNestedType("ValidationFailure");
        if (failureType != null)
        {
            // If the nested type is an open generic (e.g. Result<T>.ValidationFailure),
            // close it with the type arguments of TResponse.
            if (failureType.ContainsGenericParameters && resultType.IsGenericType)
                failureType = failureType.MakeGenericType(resultType.GetGenericArguments());

            var instance = Activator.CreateInstance(failureType, errors);
            return (TResponse)instance!;
        }

        throw new ValidationException(failures);
    }
}
