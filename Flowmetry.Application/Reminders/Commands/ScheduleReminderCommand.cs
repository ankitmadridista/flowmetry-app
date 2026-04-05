using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Domain;
using MediatR;

namespace Flowmetry.Application.Reminders.Commands;

public record ScheduleReminderCommand(
    Guid InvoiceId,
    Guid CustomerId,
    ReminderType ReminderType,
    DateTimeOffset ScheduledAt) : IRequest<Result<Guid>>;

public class ScheduleReminderCommandHandler(
    IInvoiceRepository invoiceRepository,
    IReminderRepository reminderRepository)
    : IRequestHandler<ScheduleReminderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(ScheduleReminderCommand request, CancellationToken cancellationToken)
    {
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return new Result<Guid>.NotFound($"Invoice '{request.InvoiceId}' not found.");

        var reminder = Reminder.Create(request.InvoiceId, request.CustomerId, request.ReminderType, request.ScheduledAt);

        await reminderRepository.AddAsync(reminder, cancellationToken);
        await reminderRepository.SaveChangesAsync(cancellationToken);

        return new Result<Guid>.Success(reminder.Id);
    }
}
