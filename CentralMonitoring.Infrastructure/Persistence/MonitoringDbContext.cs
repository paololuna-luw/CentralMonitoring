using CentralMonitoring.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CentralMonitoring.Infrastructure.Persistence;

public class MonitoringDbContext : DbContext
{
    public MonitoringDbContext(DbContextOptions<MonitoringDbContext> options)
        : base(options) { }

    public DbSet<Host> Hosts => Set<Host>();
    public DbSet<MetricSample> MetricSamples => Set<MetricSample>();
    public DbSet<AlertEvent> AlertEvents => Set<AlertEvent>();
    public DbSet<SnmpTarget> SnmpTargets => Set<SnmpTarget>();
    public DbSet<Rule> Rules => Set<Rule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Host>(e =>
        {
            e.HasKey(h => h.Id);

            e.Property(h => h.Name)
                .HasMaxLength(200)
                .IsRequired();

            e.Property(h => h.IpAddress)
                .HasMaxLength(45) // IPv6 cabe
                .IsRequired();

            e.Property(h => h.Type)
                .HasMaxLength(50)
                .IsRequired();

            e.Property(h => h.Tags)
                .HasMaxLength(500);

            e.Property(h => h.MetricsJson)
                .HasColumnType("text");

            e.HasIndex(h => h.IpAddress);
            e.HasIndex(h => h.Name);
        });

        modelBuilder.Entity<MetricSample>(e =>
        {
            e.HasKey(m => m.Id);

            e.Property(m => m.MetricKey)
                .HasMaxLength(120)
                .IsRequired();

            e.Property(m => m.LabelsJson)
                .HasMaxLength(2000);

            e.HasOne(m => m.Host)
                .WithMany()
                .HasForeignKey(m => m.HostId)
                .OnDelete(DeleteBehavior.Cascade);

            // índices para consultas
            e.HasIndex(m => new { m.HostId, m.TimestampUtc });
            e.HasIndex(m => new { m.HostId, m.MetricKey, m.TimestampUtc });
        });
        modelBuilder.Entity<AlertEvent>(e =>
        {
            e.HasKey(a => a.Id);

            e.Property(a => a.MetricKey)
                .HasMaxLength(120)
                .IsRequired();

            e.Property(a => a.ContextKey)
                .HasMaxLength(2000);

            e.Property(a => a.LabelsJson)
                .HasMaxLength(4000);

            e.Property(a => a.Severity)
                .HasMaxLength(50)
                .IsRequired();

            e.Property(a => a.TriggerValue);
            e.Property(a => a.LastTriggerValue);
            e.Property(a => a.Occurrences)
                .HasDefaultValue(1);
            e.Property(a => a.CreatedAtUtc);
            e.Property(a => a.LastTriggerAtUtc);
            e.Property(a => a.DispatchAttempts);
            e.Property(a => a.DispatchedAtUtc);

            e.HasOne(a => a.Host)
                .WithMany()
                .HasForeignKey(a => a.HostId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(a => new { a.HostId, a.MetricKey, a.ContextKey, a.IsResolved });
        });

        modelBuilder.Entity<SnmpTarget>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.IpAddress)
                .HasMaxLength(45)
                .IsRequired();
            e.Property(t => t.Version)
                .HasMaxLength(10)
                .IsRequired();
            e.Property(t => t.Community)
                .HasMaxLength(100);
            e.Property(t => t.SecurityName).HasMaxLength(100);
            e.Property(t => t.AuthProtocol).HasMaxLength(50);
            e.Property(t => t.AuthPassword).HasMaxLength(200);
            e.Property(t => t.PrivProtocol).HasMaxLength(50);
            e.Property(t => t.PrivPassword).HasMaxLength(200);
            e.Property(t => t.Profile).HasMaxLength(100);
            e.Property(t => t.Tags).HasMaxLength(500);
            e.Property(t => t.Enabled).HasDefaultValue(true);
            e.Property(t => t.CreatedAtUtc);
            e.Property(t => t.ConsecutiveFailures)
                .HasDefaultValue(0);
            e.Property(t => t.LastSuccessUtc);
            e.Property(t => t.LastFailureUtc);
            e.Property(t => t.MetricsJson)
                .HasColumnType("text");

            e.HasIndex(t => t.IpAddress);
            e.HasIndex(t => new { t.Enabled });
        });

        modelBuilder.Entity<Rule>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.MetricKey)
                .HasMaxLength(200)
                .IsRequired();
            e.Property(r => r.Operator)
                .HasMaxLength(5)
                .IsRequired();
            e.Property(r => r.Threshold);
            e.Property(r => r.WindowMinutes)
                .HasDefaultValue(5);
            e.Property(r => r.Severity)
                .HasMaxLength(50)
                .IsRequired();
            e.Property(r => r.SnmpIp)
                .HasMaxLength(45);
            e.Property(r => r.LabelContains)
                .HasMaxLength(500);
            e.Property(r => r.Enabled)
                .HasDefaultValue(true);
            e.Property(r => r.CreatedAtUtc);

            e.HasIndex(r => new { r.Enabled, r.MetricKey });
            e.HasIndex(r => r.HostId);
        });
    }
}
