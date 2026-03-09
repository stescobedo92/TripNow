using Microsoft.EntityFrameworkCore;
using TripNow.Domain.Reservations;

namespace TripNow.Infrastructure.Persistence;

public sealed class TripNowDbContext(DbContextOptions<TripNowDbContext> options) : DbContext(options)
{
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(r => r.Id);

            entity.Property(r => r.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(r => r.RiskEvaluationStatus).HasConversion<string>().HasMaxLength(32);
            entity.Property(r => r.CustomerEmail).HasMaxLength(320).IsRequired();
            entity.Property(r => r.TripCountry).HasMaxLength(3).IsRequired();
            entity.Property(r => r.Currency).HasMaxLength(3).IsRequired();
            entity.Property(r => r.RiskProviderRawStatus).HasMaxLength(64);
            entity.Property(r => r.RiskProviderError).HasMaxLength(512);

            entity.HasIndex(r => r.CustomerEmail);
            entity.HasIndex(r => r.CreatedAtUtc);
            entity.HasIndex(r => r.Status);

            entity.Navigation(r => r.Decisions).UsePropertyAccessMode(PropertyAccessMode.Field);

            entity.OwnsMany(r => r.Decisions, decisions =>
            {
                decisions.WithOwner().HasForeignKey("ReservationId");
                decisions.ToTable("ReservationDecisions");
                decisions.Property<int>("Id");
                decisions.HasKey("Id");
                decisions.Property(d => d.Reason).HasMaxLength(512).IsRequired();
                decisions.Property(d => d.ResultingStatus).HasConversion<string>().HasMaxLength(32);
            });
        });
    }
}
