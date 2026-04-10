using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CloudNativeImageProcessing.Infrastructure.Persistence;

public sealed class ImageDbContextFactory : IDesignTimeDbContextFactory<ImageDbContext>
{
    public ImageDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ImageDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=cloud_native_image_processing;Username=postgres;Password=postgres");
        return new ImageDbContext(optionsBuilder.Options);
    }
}
