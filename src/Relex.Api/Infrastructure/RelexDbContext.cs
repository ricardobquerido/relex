using Microsoft.EntityFrameworkCore;
using Relex.Domain;

namespace Relex.Api.Infrastructure;

public class RelexDbContext : DbContext
{
    public RelexDbContext(DbContextOptions<RelexDbContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders { get; set; }
    public DbSet<Location> Locations { get; set; }
    public DbSet<Product> Products { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 1. Locations (Lookup)
        modelBuilder.Entity<Location>(entity =>
        {
            entity.ToTable("locations");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code").IsRequired();
        });

        // 2. Products (Lookup)
        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("products");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Code).HasColumnName("code").IsRequired();
        });

        // 3. Orders (Partitioned Fact Table)
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable("orders");
            
            // Composite PK required for partitioning
            entity.HasKey(e => new { e.OrderDate, e.Id });

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.LocationId).HasColumnName("location_id");
            entity.Property(e => e.ProductId).HasColumnName("product_id");
            entity.Property(e => e.OrderDate).HasColumnName("order_date");
            entity.Property(e => e.Quantity).HasColumnName("quantity");
            entity.Property(e => e.SubmittedBy).HasColumnName("submitted_by");
            entity.Property(e => e.SubmittedAt).HasColumnName("submitted_at");
            entity.Property(e => e.Status).HasColumnName("status");

            // Relationships
            entity.HasOne(e => e.Location)
                .WithMany()
                .HasForeignKey(e => e.LocationId);

            entity.HasOne(e => e.Product)
                .WithMany()
                .HasForeignKey(e => e.ProductId);
        });
    }
}
