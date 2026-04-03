using Flowmetry.Domain;
using Flowmetry.Infrastructure.Projections;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure;

public class FlowmetryDbContext(DbContextOptions<FlowmetryDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CashflowProjection> CashflowProjections => Set<CashflowProjection>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Invoice>(builder =>
        {
            builder.ToTable("invoices");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();
            builder.Property(i => i.Status).HasConversion<string>();
            builder.Navigation(i => i.Payments).HasField("_payments").UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Ignore(i => i.DomainEvents);
        });

        modelBuilder.Entity<Payment>(builder =>
        {
            builder.ToTable("payments");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();
            builder.HasOne<Invoice>()
                   .WithMany(i => i.Payments)
                   .HasForeignKey(p => p.InvoiceId);
        });

        modelBuilder.Entity<Customer>(builder =>
        {
            builder.ToTable("customers");
            builder.HasKey(c => c.Id);
        });

        modelBuilder.Entity<CashflowProjection>(builder =>
        {
            builder.ToTable("cashflow_projections");
            builder.HasKey(p => p.InvoiceId);
            builder.Property(p => p.InvoiceId).ValueGeneratedNever();
            builder.Property(p => p.InvoiceAmount).HasColumnType("numeric(18,2)");
            builder.Property(p => p.PaidAmount).HasColumnType("numeric(18,2)");
            builder.Property(p => p.Status).HasMaxLength(32);
            builder.Property(p => p.SettledAt).HasColumnType("timestamp with time zone");
        });
    }
}
