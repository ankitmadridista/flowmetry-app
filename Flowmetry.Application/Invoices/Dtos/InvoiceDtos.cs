namespace Flowmetry.Application.Invoices.Dtos;

public record InvoiceDetailsDto(
    Guid Id,
    Guid CustomerId,
    decimal Amount,
    DateOnly DueDate,
    string Status,
    IReadOnlyList<PaymentDto> Payments);

public record PaymentDto(Guid Id, decimal Amount, DateTimeOffset RecordedAt);

public record InvoiceSummaryDto(
    Guid Id,
    Guid CustomerId,
    decimal Amount,
    DateOnly DueDate,
    string Status);
