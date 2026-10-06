using FinanceDesk.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceDesk.Data;

public class AppDbContext : DbContext
{
    private readonly IHttpContextAccessor _http;

    public AppDbContext(DbContextOptions<AppDbContext> options, IHttpContextAccessor http) : base(options)
    {
        _http = http;
    }

    public DbSet<Student> Students { get; set; }
    public DbSet<Fee> Fees { get; set; }
    public DbSet<Discount> Discounts { get; set; }
    public DbSet<Invoice> Invoices { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Receipt> Receipts { get; set; }
    public DbSet<ReminderLog> ReminderLogs { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<AuditLog> AuditLogs { get; set; }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Student>().HasIndex(s => s.StudentNo).IsUnique();
        b.Entity<Invoice>().HasIndex(i => i.InvoiceNo).IsUnique();
        b.Entity<Receipt>().HasIndex(r => r.ReceiptNo).IsUnique();
        b.Entity<User>().HasIndex(u => u.Username).IsUnique();

        b.Entity<Invoice>().HasOne(i => i.Student).WithMany(s => s.Invoices)
            .HasForeignKey(i => i.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Invoice>().HasOne(i => i.Fee).WithMany()
            .HasForeignKey(i => i.FeeId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Invoice>().HasOne(i => i.Discount).WithMany()
            .HasForeignKey(i => i.DiscountId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Payment>().HasOne(p => p.Invoice).WithMany(i => i.Payments)
            .HasForeignKey(p => p.InvoiceId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Payment>().HasOne(p => p.Receipt).WithOne(r => r.Payment)
            .HasForeignKey<Receipt>(r => r.PaymentId).OnDelete(DeleteBehavior.Cascade);
    }

    // ---------- Automatic audit trail: who created / changed / deleted what ----------
    private static readonly HashSet<string> IgnoredProps = new() { "LastLogin", "FailedAttempts", "LockoutEnd" };

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var pending = new List<(object Entity, EntityState State, string Changes)>();
        foreach (var e in ChangeTracker.Entries().ToList())
        {
            if (e.Entity is AuditLog or ReminderLog or Receipt) continue;
            if (e.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            string changes = "";
            if (e.State == EntityState.Modified)
            {
                var list = new List<string>();
                foreach (var p in e.Properties.Where(p => p.IsModified))
                {
                    var n = p.Metadata.Name;
                    if (IgnoredProps.Contains(n) || Equals(p.OriginalValue, p.CurrentValue)) continue;
                    list.Add(n == "PasswordHash" ? "Password changed" : $"{n}: {p.OriginalValue} -> {p.CurrentValue}");
                }
                if (e.Entity is User && list.Count == 0) continue;   // ignore login bookkeeping
                changes = string.Join(", ", list);
            }
            pending.Add((e.Entity, e.State, changes));
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        if (pending.Count > 0)
        {
            var user = _http.HttpContext?.User;
            var actor = user?.Identity?.IsAuthenticated == true ? user.Identity.Name : "system";
            foreach (var (entity, state, changes) in pending)
            {
                var text = Describe(entity) + (string.IsNullOrEmpty(changes) ? "" : " | " + changes);
                AuditLogs.Add(new AuditLog
                {
                    Username = actor,
                    Action = state == EntityState.Added ? "Created" : state == EntityState.Deleted ? "Deleted" : "Updated",
                    Entity = entity.GetType().Name,
                    Details = text.Length > 480 ? text[..480] : text
                });
            }
            await base.SaveChangesAsync(cancellationToken);
        }
        return result;
    }

    private static string Describe(object e) => e switch
    {
        Invoice i => $"Invoice {i.InvoiceNo} ({i.Total.Money()})",
        Payment p => $"Payment {p.Amount.Money()} via {p.Method} (invoice #{p.InvoiceId})",
        Student s => $"Student {s.StudentNo} - {s.FullName}",
        Fee f => $"Fee '{f.Name}' ({f.Amount.Money()})",
        Discount d => $"Discount '{d.Name}'",
        User u => $"User '{u.Username}' ({u.Role})",
        _ => e.GetType().Name
    };
}
