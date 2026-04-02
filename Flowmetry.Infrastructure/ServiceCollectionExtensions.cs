using Flowmetry.Application.Invoices;
using Flowmetry.Infrastructure.Persistence;
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
        var rawUrl = configuration["DATABASE_URL"]
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? throw new InvalidOperationException(
                "DATABASE_URL environment variable is not set. " +
                "Please configure a PostgreSQL connection string.");

        var connectionString = ConvertToNpgsqlConnectionString(rawUrl);

        services.AddDbContext<FlowmetryDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IInvoiceRepository, InvoiceRepository>();

        return services;
    }

    internal static string ConvertUri(string url) => ConvertToNpgsqlConnectionString(url);

    /// <summary>
    /// Converts a postgresql:// URI to Npgsql key=value connection string format.
    /// e.g. postgresql://user:pass@host/db?sslmode=require → Host=host;Database=db;Username=user;Password=pass;SSL Mode=Require
    /// </summary>
    private static string ConvertToNpgsqlConnectionString(string url)
    {
        if (!url.StartsWith("postgresql://") && !url.StartsWith("postgres://"))
            return url; // already key=value format, pass through

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 5432;
        var database = uri.AbsolutePath.TrimStart('/');

        var builder = new System.Text.StringBuilder(
            $"Host={host};Port={port};Database={database};Username={username};Password={password}");

        // Parse query string params (e.g. sslmode=require, channel_binding=require)
        var query = uri.Query.TrimStart('?');
        foreach (var param in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = param.Split('=', 2);
            if (kv.Length != 2) continue;
            switch (kv[0].ToLowerInvariant())
            {
                case "sslmode":
                    // Npgsql uses "SSL Mode" with Pascal-cased values
                    var mode = kv[1][0..1].ToUpper() + kv[1][1..].ToLower();
                    builder.Append($";SSL Mode={mode}");
                    break;
                case "channel_binding":
                    // not a standard Npgsql key, skip
                    break;
            }
        }

        return builder.ToString();
    }
}
