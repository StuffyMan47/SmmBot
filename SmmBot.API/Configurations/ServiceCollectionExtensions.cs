using Microsoft.EntityFrameworkCore;
using SmmBot.Infrastructure.DAL.DbContext;
using SmmBot.Infrastructure.Services.Storage;
using SmmBot.Core.Interfaces.Storage;
using SmmBot.Core.Interfaces.Settings.Models;

using SmmBot.Core.Interfaces.Ai;
using SmmBot.Infrastructure.Services.Ai;

namespace SmmBot.API.Configurations;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DBConnectionString")));

        services.Configure<BotConfiguration>(configuration.GetSection("BotConfiguration"));
        services.Configure<S3StorageSettings>(configuration.GetSection("S3Storage"));

        services.AddScoped<IS3StorageService, S3StorageService>();
        services.AddHttpClient<IAiService, RouterAiService>();

        return services;
    }
}
