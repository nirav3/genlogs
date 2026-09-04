using Genlogs.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Genlogs.Api.Data;

public class GenlogsDbContext : DbContext
{
    public GenlogsDbContext(DbContextOptions<GenlogsDbContext> options) : base(options)
    {
    }

    public DbSet<Carrier> Carriers => Set<Carrier>();
    public DbSet<Lane> Lanes => Set<Lane>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<DetectionEvent> DetectionEvents => Set<DetectionEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // design.md names this PK "DetectionId", which doesn't match EF's "<TypeName>Id" convention for
        // the "DetectionEvent" entity — configured explicitly to keep the schema's documented column name.
        modelBuilder.Entity<DetectionEvent>().HasKey(d => d.DetectionId);

        modelBuilder.Entity<Lane>()
            .HasIndex(l => new { l.OriginCity, l.DestinationCity })
            .IsUnique();

        modelBuilder.Entity<Vehicle>()
            .HasOne(v => v.Carrier)
            .WithMany(c => c.Vehicles)
            .HasForeignKey(v => v.CarrierId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DetectionEvent>()
            .HasOne(d => d.Lane)
            .WithMany(l => l.DetectionEvents)
            .HasForeignKey(d => d.LaneId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<DetectionEvent>()
            .HasOne(d => d.Vehicle)
            .WithMany(v => v.DetectionEvents)
            .HasForeignKey(d => d.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
