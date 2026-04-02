using CsCheck;
using Flowmetry.Domain;
using Flowmetry.Domain.Events;

namespace Flowmetry.Domain.Tests.PropertyTests;

public class InvoicePropertyTests
{
    // ── helpers ──────────────────────────────────────────────────────────────

    private static Invoice CreateDraftInvoice(decimal amount = 100m) =>
        InvoiceTestHelper.CreateDraftInvoice(amount: amount);

    private static Invoice CreateSentInvoice(decimal amount = 100m) =>
        InvoiceTestHelper.CreateSentInvoice(amount: amount);

    private static Invoice CreateInvoiceInStatus(InvoiceStatus status, decimal invoiceAmount = 200m)
    {
        return status switch
        {
            InvoiceStatus.Draft        => InvoiceTestHelper.CreateDraftInvoice(amount: invoiceAmount),
            InvoiceStatus.Sent         => InvoiceTestHelper.CreateSentInvoice(amount: invoiceAmount),
            InvoiceStatus.PartiallyPaid => InvoiceTestHelper.CreatePartiallyPaidInvoice(invoiceAmount: invoiceAmount, paymentAmount: invoiceAmount / 2),
            InvoiceStatus.Paid         => InvoiceTestHelper.CreatePaidInvoice(amount: invoiceAmount),
            InvoiceStatus.Overdue      => InvoiceTestHelper.CreateOverdueInvoice(amount: invoiceAmount),
            _ => throw new ArgumentOutOfRangeException(nameof(status))
        };
    }

    // ── Property 1 ───────────────────────────────────────────────────────────

    [Fact]
    public void Property1_InvoiceCreate_AlwaysDraftWithEmptyPayments()
    {
        // Feature: invoice-domain, Property 1: Invoice.Create always produces Draft with empty Payments
        Gen.Select(Gen.Guid, Gen.Decimal[1m, 10000m])
           .Sample((customerId, amount) =>
           {
               var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
               var invoice = Invoice.Create(customerId, amount, dueDate);
               Assert.Equal(InvoiceStatus.Draft, invoice.Status);
               Assert.Empty(invoice.Payments);
           });
    }

    // ── Property 2 ───────────────────────────────────────────────────────────

    [Fact]
    public void Property2_MarkAsSent_DraftToSent_NonDraftThrows()
    {
        // Feature: invoice-domain, Property 2: MarkAsSent transitions Draft→Sent; throws for non-Draft
        // Test happy path: Draft → Sent
        Gen.Select(Gen.Guid, Gen.Decimal[1m, 10000m])
           .Sample((customerId, amount) =>
           {
               var invoice = Invoice.Create(customerId, amount, DateOnly.FromDateTime(DateTime.Today.AddDays(30)));
               invoice.MarkAsSent();
               Assert.Equal(InvoiceStatus.Sent, invoice.Status);
               Assert.Contains(invoice.DomainEvents, e => e is InvoiceSent);
           });

        // Test guard: non-Draft throws
        var nonDraftStatuses = new[] { InvoiceStatus.Sent, InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid, InvoiceStatus.Overdue };
        foreach (var status in nonDraftStatuses)
        {
            var invoice = CreateInvoiceInStatus(status);
            Assert.Throws<InvalidOperationException>(() => invoice.MarkAsSent());
        }
    }

    // ── Property 3 ───────────────────────────────────────────────────────────

    [Fact]
    public void Property3_RecordPayment_NonPositiveAmount_ThrowsArgumentException()
    {
        // Feature: invoice-domain, Property 3: RecordPayment(amount <= 0) throws ArgumentException
        // Generate non-positive amounts: negate a positive value, then also test 0
        Gen.Decimal[0.01m, 1000m]
           .Select(v => -v)
           .Sample(amount =>
           {
               var invoice = CreateSentInvoice(100m);
               Assert.Throws<ArgumentException>(() => invoice.RecordPayment(amount));
           });
        // Also test exactly 0
        {
            var invoice = CreateSentInvoice(100m);
            Assert.Throws<ArgumentException>(() => invoice.RecordPayment(0m));
        }
    }

    // ── Property 4 ───────────────────────────────────────────────────────────

    [Fact]
    public void Property4_RecordPayment_Overpayment_ThrowsInvalidOperationException()
    {
        // Feature: invoice-domain, Property 4: overpayment throws InvalidOperationException
        Gen.Decimal[1m, 100m]
           .Sample(invoiceAmount =>
           {
               var invoice = CreateSentInvoice(invoiceAmount);
               var overpayment = invoiceAmount + 0.01m;
               Assert.Throws<InvalidOperationException>(() => invoice.RecordPayment(overpayment));
           });
    }

    // ── Property 5 ───────────────────────────────────────────────────────────

    [Fact]
    public void Property5_RecordPayment_StatusTransitions_AreCorrect()
    {
        // Feature: invoice-domain, Property 5: payment status transitions (PartiallyPaid vs Paid) are correct
        Gen.Select(Gen.Decimal[10m, 1000m], Gen.Decimal[1m, 9m])
           .Sample((invoiceAmount, partialFraction) =>
           {
               var partialAmount = Math.Round(invoiceAmount * partialFraction / 10m, 2);
               if (partialAmount <= 0 || partialAmount >= invoiceAmount) return;

               var invoice = CreateSentInvoice(invoiceAmount);
               invoice.RecordPayment(partialAmount);
               Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);

               var remaining = invoiceAmount - partialAmount;
               invoice.RecordPayment(remaining);
               Assert.Equal(InvoiceStatus.Paid, invoice.Status);
           });
    }

    // ── Property 6 ───────────────────────────────────────────────────────────

    [Fact]
    public void Property6_RecordPayment_NonPayableStatus_ThrowsInvalidOperationException()
    {
        // Feature: invoice-domain, Property 6: RecordPayment on non-payable status throws
        var nonPayableStatuses = new[] { InvoiceStatus.Draft, InvoiceStatus.Paid, InvoiceStatus.Overdue };
        Gen.Decimal[1m, 100m]
           .Sample(amount =>
           {
               foreach (var status in nonPayableStatuses)
               {
                   var invoice = CreateInvoiceInStatus(status, invoiceAmount: 200m);
                   Assert.Throws<InvalidOperationException>(() => invoice.RecordPayment(amount));
               }
           });
    }

    // ── Property 7 ───────────────────────────────────────────────────────────

    [Fact]
    public void Property7_MarkOverdue_ValidTransitions_AndGuards()
    {
        // Feature: invoice-domain, Property 7: MarkOverdue transitions Sent/PartiallyPaid→Overdue; throws otherwise
        Gen.Decimal[1m, 1000m]
           .Sample(amount =>
           {
               // Valid: Sent → Overdue
               var sentInvoice = CreateSentInvoice(amount);
               sentInvoice.MarkOverdue();
               Assert.Equal(InvoiceStatus.Overdue, sentInvoice.Status);

               // Valid: PartiallyPaid → Overdue
               var partialInvoice = CreateSentInvoice(amount);
               partialInvoice.RecordPayment(amount / 2);
               partialInvoice.MarkOverdue();
               Assert.Equal(InvoiceStatus.Overdue, partialInvoice.Status);

               // Invalid: Draft throws
               var draftInvoice = CreateDraftInvoice(amount);
               Assert.Throws<InvalidOperationException>(() => draftInvoice.MarkOverdue());
           });
    }

    // ── Property 8 ───────────────────────────────────────────────────────────

    [Fact]
    public void Property8_DomainEventPayloads_MatchTriggeringData()
    {
        // Feature: invoice-domain, Property 8: domain event payloads match triggering data
        Gen.Select(Gen.Guid, Gen.Decimal[1m, 10000m])
           .Sample((customerId, amount) =>
           {
               var dueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(30));
               var invoice = Invoice.Create(customerId, amount, dueDate);

               var created = invoice.DomainEvents.OfType<InvoiceCreated>().Single();
               Assert.Equal(invoice.Id, created.InvoiceId);
               Assert.Equal(customerId, created.CustomerId);
               Assert.Equal(amount, created.Amount);
               Assert.Equal(dueDate, created.DueDate);

               invoice.MarkAsSent();
               var sent = invoice.DomainEvents.OfType<InvoiceSent>().Single();
               Assert.Equal(invoice.Id, sent.InvoiceId);
               Assert.NotEqual(default, sent.SentAt);
           });
    }

    // ── Property 9 ───────────────────────────────────────────────────────────

    [Fact]
    public void Property9_AccumulatedPayments_TotalAndStatus_AreCorrect()
    {
        // Feature: invoice-domain, Property 9: accumulated payments total and status are correct after any valid sequence
        Gen.Select(Gen.Decimal[10m, 1000m], Gen.Int[2, 5])
           .Sample((invoiceAmount, paymentCount) =>
           {
               var invoice = CreateSentInvoice(invoiceAmount);
               var remaining = invoiceAmount;
               var totalPaid = 0m;

               for (int i = 0; i < paymentCount - 1; i++)
               {
                   var payment = Math.Round(remaining / (paymentCount - i), 2);
                   if (payment <= 0 || payment > remaining) break;
                   invoice.RecordPayment(payment);
                   totalPaid += payment;
                   remaining -= payment;
                   Assert.Equal(totalPaid, invoice.Payments.Sum(p => p.Amount));
                   if (totalPaid < invoiceAmount)
                       Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
               }

               if (remaining > 0)
               {
                   invoice.RecordPayment(remaining);
                   totalPaid += remaining;
                   Assert.Equal(invoiceAmount, invoice.Payments.Sum(p => p.Amount));
                   Assert.Equal(InvoiceStatus.Paid, invoice.Status);
               }
           });
    }
}
