using Flowmetry.Domain;
using Flowmetry.Infrastructure.Identity;
using Flowmetry.Infrastructure.Projections;
using Flowmetry.Infrastructure.Security;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Flowmetry.Infrastructure;

public class FlowmetryDbContext(DbContextOptions<FlowmetryDbContext> options)
    : IdentityDbContext<AppUser>(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CashflowProjection> CashflowProjections => Set<CashflowProjection>();
    public DbSet<Reminder> Reminders => Set<Reminder>();

    public DbSet<SecurityObject> SecurityObjects => Set<SecurityObject>();
    public DbSet<SecurityOperation> SecurityOperations => Set<SecurityOperation>();
    public DbSet<SecurityPermission> SecurityPermissions => Set<SecurityPermission>();
    public DbSet<SecurityRole> SecurityRoles => Set<SecurityRole>();
    public DbSet<SecurityRolePermission> SecurityRolePermissions => Set<SecurityRolePermission>();
    public DbSet<SecurityRoleUser> SecurityRoleUsers => Set<SecurityRoleUser>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Invoice>(builder =>
        {
            builder.ToTable("invoices");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Id).ValueGeneratedNever();
            builder.Property(i => i.InvoiceNumber)
                   .UseIdentityByDefaultColumn()
                   .ValueGeneratedOnAdd();
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

        modelBuilder.Entity<Reminder>(builder =>
        {
            builder.ToTable("reminders");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();
            builder.Property(r => r.ReminderType).HasConversion<string>();
            builder.Property(r => r.Status).HasConversion<string>();
            builder.HasIndex(r => new { r.Status, r.ScheduledAt });
        });

        modelBuilder.Entity<SecurityObject>(b =>
        {
            b.ToTable("security_objects");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).UseIdentityByDefaultColumn();
            b.HasOne(x => x.Parent).WithMany().HasForeignKey(x => x.ParentId);
        });

        modelBuilder.Entity<SecurityOperation>(b =>
        {
            b.ToTable("security_operations");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).UseIdentityByDefaultColumn();
        });

        modelBuilder.Entity<SecurityPermission>(b =>
        {
            b.ToTable("security_permissions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).UseIdentityByDefaultColumn();
        });

        modelBuilder.Entity<SecurityRole>(b =>
        {
            b.ToTable("security_roles");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).UseIdentityByDefaultColumn();
        });

        modelBuilder.Entity<SecurityRolePermission>(b =>
        {
            b.ToTable("security_role_permissions");
            b.HasKey(x => new { x.RoleId, x.PermissionId });
        });

        modelBuilder.Entity<SecurityRoleUser>(b =>
        {
            b.ToTable("security_role_users");
            b.HasKey(x => new { x.RoleId, x.UserId });
        });
    }
}
