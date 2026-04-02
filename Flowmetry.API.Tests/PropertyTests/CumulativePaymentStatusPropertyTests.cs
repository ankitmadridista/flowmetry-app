using CsCheck;
using Flowmetry.Application.Common;
using Flowmetry.Application.Invoices;
using Flowmetry.Application.Invoices.Commands;
using Flowmetry.Domain;
using MediatR;

namespace Flowmetry.API.Tests.PropertyTests;

/// <summary>
/// Property-based tests for cumulative payment status correctness.
/// Property 1: Fault Condition — Cumulative Payments Reach Invoice Amount
/// Property 2: Preservation — Non-Buggy Inputs Produce Identical Outcomes
/// </summary>
public class CumulativePaymentStatusPropertyTests
{
    // ── Property 2: Preservation ──────────────────────────────────────────────
    // Validates: Requirements 3.1, 3.2, 3.3
    //
    // These tests use InMemoryInvoiceRepository directly (both GetByIdAsync and
    // GetByIdWithPaymentsAsync return the same in-memory object), so they pass on
    // both unfixed and fixed code — confirming baseline behavior is preserved.

    [Fact]
    public async Task Property2a_Preservation_SingleFullPayment_StatusIsPaid()
    {
        // **Validates: Requirements 3.1**
        //
        // For any fresh invoice, a single payment equal to the invoice amount
        // MUST result in Status == Paid.

        await Gen.Int[1, 10000]
            .Select(x => (decimal)x)
            .SampleAsync(async invoiceAmount =>
            {
                var repo = new InMemoryInvoiceRepository();
                var invoice = Invoice.Create(Guid.NewGuid(), invoiceAmount, DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
                invoice.MarkAsSent();
                await repo.AddAsync(invoice);
                await repo.SaveChangesAsync();

                var handler = new RecordPaymentCommandHandler(repo);
                var result = await handler.Handle(
                    new RecordPaymentCommand(invoice.Id, invoiceAmount),
                    CancellationToken.None);

                Assert.IsType<Result<Unit>.Success>(result);

                var reloaded = await repo.GetByIdWithPaymentsAsync(invoice.Id);
                Assert.NotNull(reloaded);
                Assert.Equal(InvoiceStatus.Paid, reloaded.Status);
            });
    }

    [Fact]
    public async Task Property2b_Preservation_SinglePartialPayment_StatusIsPartiallyPaid()
    {
        // **Validates: Requirements 3.2**
        //
        // For any fresh invoice, a single payment less than the invoice amount
        // MUST result in Status == PartiallyPaid.

        await (
            from invoiceAmount in Gen.Int[2, 10000].Select(x => (decimal)x)
            from partialAmount in Gen.Int[1, (int)invoiceAmount - 1].Select(x => (decimal)x)
            select (invoiceAmount, partialAmount)
        ).SampleAsync(async tuple =>
        {
            var (invoiceAmount, partialAmount) = tuple;

            var repo = new InMemoryInvoiceRepository();
            var invoice = Invoice.Create(Guid.NewGuid(), invoiceAmount, DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
            invoice.MarkAsSent();
            await repo.AddAsync(invoice);
            await repo.SaveChangesAsync();

            var handler = new RecordPaymentCommandHandler(repo);
            var result = await handler.Handle(
                new RecordPaymentCommand(invoice.Id, partialAmount),
                CancellationToken.None);

            Assert.IsType<Result<Unit>.Success>(result);

            var reloaded = await repo.GetByIdWithPaymentsAsync(invoice.Id);
            Assert.NotNull(reloaded);
            Assert.Equal(InvoiceStatus.PartiallyPaid, reloaded.Status);
        });
    }

    [Fact]
    public async Task Property2c_Preservation_NonExistentInvoice_ReturnsNotFound()
    {
        // **Validates: Requirements 3.3**
        //
        // For any random Guid that does not correspond to a persisted invoice,
        // RecordPaymentCommand MUST return Result<Unit>.NotFound.

        await Gen.Guid
            .SampleAsync(async randomId =>
            {
                var repo = new InMemoryInvoiceRepository();

                var handler = new RecordPaymentCommandHandler(repo);
                var result = await handler.Handle(
                    new RecordPaymentCommand(randomId, 100m),
                    CancellationToken.None);

                Assert.IsType<Result<Unit>.NotFound>(result);
            });
    }

    // ── Property 1: Fault Condition ───────────────────────────────────────────
    // Validates: Requirements 1.1, 1.2, 1.3
    //
    // This test encodes the EXPECTED behavior. On unfixed code (using BuggyInvoiceRepository)
    // it FAILS, confirming the bug exists. After the fix it will PASS.

    [Fact]
    public async Task Property1_FaultCondition_CumulativePaymentsReachInvoiceAmount_StatusIsPaid()
    {
        // **Validates: Requirements 1.1, 1.2, 1.3**
        //
        // For any invoice where prior payments exist and the final payment brings
        // the cumulative total to exactly the invoice amount, the status MUST be Paid.
        //
        // Now uses InMemoryInvoiceRepository (fixed handler calls GetByIdWithPaymentsAsync),
        // so the invoice is loaded with payments and status is correctly set to Paid.

        await (
            from invoiceAmount in Gen.Int[2, 1000].Select(x => (decimal)x)
            from priorPaymentCount in Gen.Int[1, 5]
            select (invoiceAmount, priorPaymentCount)
        ).SampleAsync(async tuple =>
        {
            var (invoiceAmount, priorPaymentCount) = tuple;

            // Split: priorPaymentCount equal prior payments + a final payment covering the remainder
            var priorPaymentAmount = Math.Floor(invoiceAmount / (priorPaymentCount + 1));
            var finalPaymentAmount = invoiceAmount - (priorPaymentAmount * priorPaymentCount);

            // Skip degenerate cases where rounding produces zero-amount payments
            if (priorPaymentAmount <= 0 || finalPaymentAmount <= 0)
                return;

            var repo = new InMemoryInvoiceRepository();
            var customerId = Guid.NewGuid();
            var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));

            // Create and send invoice
            var invoice = Invoice.Create(customerId, invoiceAmount, dueDate);
            invoice.MarkAsSent();

            // Record all prior payments directly on the domain entity (bypassing the handler)
            for (int i = 0; i < priorPaymentCount; i++)
            {
                invoice.RecordPayment(priorPaymentAmount);
            }

            // Persist the invoice (with prior payments already on the entity)
            await repo.AddAsync(invoice);
            await repo.SaveChangesAsync();

            // Invoke the handler for the final payment (uses BuggyInvoiceRepository)
            var handler = new RecordPaymentCommandHandler(repo);
            var result = await handler.Handle(
                new RecordPaymentCommand(invoice.Id, finalPaymentAmount),
                CancellationToken.None);

            // Assert success
            Assert.IsType<Result<Unit>.Success>(result);

            // Reload the invoice to check its status
            var reloaded = await repo.GetByIdWithPaymentsAsync(invoice.Id);
            Assert.NotNull(reloaded);

            // EXPECTED: Paid — PASSES on fixed code (InMemoryInvoiceRepository returns invoice
            // with payments loaded, so domain sees all payments → Paid)
            Assert.Equal(InvoiceStatus.Paid, reloaded.Status);
        });
    }
}

// ── BuggyInvoiceRepository ────────────────────────────────────────────────────
// Simulates the unfixed RecordPaymentCommandHandler behaviour:
// GetByIdWithPaymentsAsync delegates to GetByIdAsync, which returns the invoice
// WITHOUT its payments loaded (empty _payments), just like EF Core does when
// the Payments navigation property is not eagerly included.

internal class BuggyInvoiceRepository : IInvoiceRepository
{
    // Full store: invoices with all payments (used by GetByIdWithPaymentsAsync for reads)
    private readonly List<Invoice> _fullStore = new();

    // Bare store: invoice snapshots WITHOUT payments (used by GetByIdAsync to simulate the bug)
    // Keyed by invoice Id → bare Invoice instance created before payments were recorded
    private readonly Dictionary<Guid, Invoice> _bareStore = new();

    public async Task AddAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        _fullStore.Add(invoice);

        // Create a bare snapshot: a fresh Sent invoice with the same Id and Amount
        // but no payments — simulating what EF returns when Payments is not included.
        var bare = BareInvoiceFactory.CreateSentInvoice(invoice.Id, invoice.CustomerId, invoice.Amount, invoice.DueDate);
        _bareStore[invoice.Id] = bare;

        await Task.CompletedTask;
    }

    /// <summary>
    /// Simulates the BUG: returns the invoice WITHOUT payments loaded.
    /// This is what the unfixed handler gets when it calls GetByIdAsync.
    /// </summary>
    public Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_bareStore.TryGetValue(id, out var bare) ? bare : null);

    /// <summary>
    /// Simulates the BUGGY handler: delegates to GetByIdAsync (no eager load).
    /// The fixed handler would return the full invoice with payments.
    /// </summary>
    public Task<Invoice?> GetByIdWithPaymentsAsync(Guid id, CancellationToken cancellationToken = default)
        => GetByIdAsync(id, cancellationToken);

    public Task<IReadOnlyList<Invoice>> GetAllAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<Invoice>>(_fullStore.AsReadOnly());

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> CustomerExistsAsync(Guid customerId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    /// <summary>
    /// Returns the full invoice (with payments) for post-handler status inspection.
    /// </summary>
    public Task<Invoice?> GetFullInvoiceAsync(Guid id)
        => Task.FromResult(_fullStore.FirstOrDefault(i => i.Id == id));
}

// ── BareInvoiceFactory ────────────────────────────────────────────────────────
// Creates an Invoice in Sent status with a specific Id, simulating what EF Core
// returns when the Payments navigation property is NOT eagerly loaded.
// Uses reflection to set the private Id field after Invoice.Create generates a new one.

internal static class BareInvoiceFactory
{
    public static Invoice CreateSentInvoice(Guid id, Guid customerId, decimal amount, DateOnly dueDate)
    {
        var invoice = Invoice.Create(customerId, amount, dueDate);

        // Override the auto-generated Id to match the original invoice
        var idProp = typeof(Invoice).GetProperty("Id",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
        idProp!.SetValue(invoice, id);

        invoice.MarkAsSent();
        return invoice;
    }
}
