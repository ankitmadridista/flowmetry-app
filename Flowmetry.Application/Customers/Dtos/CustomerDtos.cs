namespace Flowmetry.Application.Customers.Dtos;

// Returned by GetCustomerQuery and items in GetAllCustomersQuery
public record CustomerSummaryDto(
    Guid   Id,
    string Name,
    string Email,
    int    RiskScore,
    string RiskBand);   // "Low" | "Medium" | "High"

// Returned by CreateCustomerCommand (just the new id)
public record CustomerCreatedDto(Guid Id);

// Invoice projection used by GetCustomerInvoicesQuery
public record CustomerInvoiceSummaryDto(
    Guid     Id,
    decimal  Amount,
    DateOnly DueDate,
    string   Status);
