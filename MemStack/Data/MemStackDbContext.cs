using MemStack.Model;
using Microsoft.EntityFrameworkCore;

namespace MemStack.Data;

public class MemStackDbContext(DbContextOptions<MemStackDbContext> options) : DbContext(options)
{
    public DbSet<FeatureMemory> FeatureMemories => Set<FeatureMemory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<FeatureMemory>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ExternalFeatureId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.SourceSystem).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CustomerName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Status).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Tags).HasMaxLength(500);
        });
    }
}

