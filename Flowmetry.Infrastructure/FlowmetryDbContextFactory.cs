using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Flowmetry.Infrastructure;

public class FlowmetryDbContextFactory : IDesignTimeDbContextFactory<FlowmetryDbContext>
{
    public FlowmetryDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FlowmetryDbContext>();

        var rawUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? "Host=localhost;Database=flowmetry_design;Username=postgres;Password=postgres";

        var connectionString = ServiceCollectionExtensions.ConvertUri(rawUrl);
        optionsBuilder.UseNpgsql(connectionString);

        return new FlowmetryDbContext(optionsBuilder.Options);
    }
}
