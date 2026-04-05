using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using MediatR;

namespace Flowmetry.Application.Reminders.Queries;

public record ReminderDto(
    Guid Id,
    Guid InvoiceId,
    Guid CustomerId,
    string ReminderType,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? SentAt,
    string Status);

public record GetRemindersForInvoiceQuery(Guid InvoiceId) : IRequest<Result<IReadOnlyList<ReminderDto>>>;

public class GetRemindersForInvoiceQueryHandler(
    IInvoiceRepository invoiceRepository,
    IReminderRepository reminderRepository)
    : IRequestHandler<GetRemindersForInvoiceQuery, Result<IReadOnlyList<ReminderDto>>>
{
    public async Task<Result<IReadOnlyList<ReminderDto>>> Handle(
        GetRemindersForInvoiceQuery request,
        CancellationToken cancellationToken)
    {
        var invoice = await invoiceRepository.GetByIdAsync(request.InvoiceId, cancellationToken);
        if (invoice is null)
            return new Result<IReadOnlyList<ReminderDto>>.NotFound($"Invoice '{request.InvoiceId}' not found.");

        var reminders = await reminderRepository.GetByInvoiceIdAsync(request.InvoiceId, cancellationToken);

        var dtos = reminders
            .Select(r => new ReminderDto(
                r.Id,
                r.InvoiceId,
                r.CustomerId,
                r.ReminderType.ToString(),
                r.ScheduledAt,
                r.SentAt,
                r.Status.ToString()))
            .ToList();

        return new Result<IReadOnlyList<ReminderDto>>.Success(dtos);
    }
}
