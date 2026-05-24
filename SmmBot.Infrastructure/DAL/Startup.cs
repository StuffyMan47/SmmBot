using SmmBot.Infrastructure.DAL.DbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace SmmBot.Infrastructure.DAL;

public static class Startup
{
    internal static IServiceCollection AddDataAccessLayer(this IServiceCollection services, IConfiguration config, IWebHostEnvironment environment)
    {
        services.AddAppDbContext(config, environment);

        return services;
    }
    
    private static IServiceCollection AddAppDbContext(this IServiceCollection services, IConfiguration config, IWebHostEnvironment environment)
    {
        string? dbConnectionString = config.GetConnectionString("DBConnectionString");
        return services.AddDbContext<AppDbContext>(options =>
        {
            options.UseNpgsql(dbConnectionString, x => x
                .MigrationsAssembly(typeof(AppDbContext).Assembly.ToString())
                .MigrationsHistoryTable("__EFMigrationsHistory", "public")
            );
            options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTrackingWithIdentityResolution);
            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", false);

            if (!environment.IsProduction())
            {
                options
                    .UseLoggerFactory(LoggerFactory.Create(loggingBuilder => loggingBuilder.AddDebug()))
                    .EnableSensitiveDataLogging();
            }
        });
    }
}