using HTKISCloudOffice.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HTKISCloudOffice.Infrastructure.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql("Host=127.0.0.1;Port=5432;Database=htkis_cloud;Username=htkis;Password=Htkis2024!");
        return new AppDbContext(optionsBuilder.Options);
    }
}