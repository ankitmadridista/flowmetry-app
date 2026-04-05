using Flowmetry.Domain;

namespace Flowmetry.Application.RiskScoring;

public interface IRiskProfileRepository
{
    /// <summary>
    /// Returns all non-cancelled invoices for the customer, each with their
    /// Payments collection eagerly loaded. Returns an empty list when the
    /// customer has no invoices; never throws for a missing customer.
    /// </summary>
    Task<IReadOnlyList<Invoice>> GetInvoicesWithPaymentsByCustomerAsync(
        Guid customerId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns true when a Customer row with the given id exists in the database.
    /// Used to distinguish "customer not found" from "customer has no invoices".
    /// </summary>
    Task<bool> CustomerExistsAsync(
        Guid customerId,
        CancellationToken ct = default);
}
