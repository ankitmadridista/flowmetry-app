namespace Flowmetry.Application.Invoices.Dtos;

public record InvoiceDetailsDto(
    Guid Id,
    int InvoiceNumber,
    Guid CustomerId,
    string CustomerName,
    decimal Amount,
    DateOnly DueDate,
    string Status,
    IReadOnlyList<PaymentDto> Payments);

public record PaymentDto(Guid Id, decimal Amount, DateTimeOffset RecordedAt);

public record InvoiceSummaryDto(
    Guid Id,
    int InvoiceNumber,
    string CustomerName,
    decimal Amount,
    DateOnly DueDate,
    string Status);
