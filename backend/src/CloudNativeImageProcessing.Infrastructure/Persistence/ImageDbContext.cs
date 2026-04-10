using CloudNativeImageProcessing.Domain.Entities;
using CloudNativeImageProcessing.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CloudNativeImageProcessing.Infrastructure.Persistence;

public sealed class ImageDbContext : IdentityDbContext<IdentityUser>
{
    public ImageDbContext(DbContextOptions<ImageDbContext> options) : base(options)
    {
    }

    public DbSet<ImageRecord> Images => Set<ImageRecord>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var image = modelBuilder.Entity<ImageRecord>();
        image.ToTable("images");
        image.HasKey(x => x.Id);
        image.Property(x => x.Id).ValueGeneratedNever();
        image.Property(x => x.UserId).HasMaxLength(100).IsRequired();
        image.Property(x => x.Name).HasMaxLength(255).IsRequired();
        image.Property(x => x.BlobPath).HasMaxLength(500).IsRequired();
        image.Property(x => x.PreviewUrl).HasMaxLength(1000);
        image.Property(x => x.Description).HasMaxLength(2000);
        image.Property(x => x.Status).HasMaxLength(50).IsRequired();
        image.Property(x => x.Operation).HasConversion<string>().HasMaxLength(50).IsRequired();
        image.Property(x => x.CreatedAtUtc).IsRequired();
        image.HasIndex(x => new { x.UserId, x.CreatedAtUtc });
    }
}
