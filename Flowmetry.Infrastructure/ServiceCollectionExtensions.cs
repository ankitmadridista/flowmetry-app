using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Flowmetry.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["DATABASE_URL"]
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? throw new InvalidOperationException(
                "DATABASE_URL environment variable is not set. " +
                "Please configure a PostgreSQL connection string.");

        services.AddDbContext<FlowmetryDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
