using Flowmetry.Domain;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure;

public class FlowmetryDbContext(DbContextOptions<FlowmetryDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Payment> Payments => Set<Payment>();

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
    }
}
