using Flowmetry.Domain;

namespace Flowmetry.Application.Invoices;

public record InvoiceFilter(
    Guid?          CustomerId   = null,
    string?        CustomerName = null,
    InvoiceStatus? Status       = null,
    DateOnly?      DueDateFrom  = null,
    DateOnly?      DueDateTo    = null,
    int            Page         = 0,
    int            PageSize     = 25,
    SortField      SortBy       = SortField.DueDate,
    SortDirection  SortDir      = SortDirection.Asc
);

public enum SortField     { DueDate, Amount, Status }
public enum SortDirection { Asc, Desc }
