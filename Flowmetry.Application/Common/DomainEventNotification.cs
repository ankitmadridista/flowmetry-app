using Flowmetry.Domain;
using MediatR;

namespace Flowmetry.Application.Common;

public record DomainEventNotification<TEvent>(TEvent Event) : INotification
    where TEvent : IDomainEvent;
