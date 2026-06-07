using Hangfire;
using Hangfire.PostgreSql;
using SmmBot.Bot.BackgroundJobs;

namespace SmmBot.API.Configurations;

public static class HangfireConfiguration
{
    public static IServiceCollection AddHangfireServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DBConnectionString");

        services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                  .UseSimpleAssemblyNameTypeSerializer()
                  .UseRecommendedSerializerSettings()
                  .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString), new PostgreSqlStorageOptions
                  {
                      DistributedLockTimeout = TimeSpan.FromMinutes(1),
                      PrepareSchemaIfNecessary = true,
                  });
        });

        services.AddHangfireServer();

        return services;
    }

    public static void ConfigureHangfireJobs(this WebApplication app)
    {
        var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

        // Content plan generation: Every Friday at 12:00 (Cron: "0 12 * * 5")
        recurringJobManager.AddOrUpdate<ContentPlanGenerationJob>(
            "content-plan-generation",
            job => job.GeneratePlanForNextWeekAsync(CancellationToken.None),
            "0 12 * * 5"
        );

        // Post verification reminders: Every minute to check if any post is within 20 mins
        recurringJobManager.AddOrUpdate<PostVerificationJob>(
            "post-verification-reminders",
            job => job.SendVerificationRemindersAsync(CancellationToken.None),
            "* * * * *"
        );

        // Post publisher: Every minute to check if any confirmed post needs publishing
        recurringJobManager.AddOrUpdate<PostPublisherJob>(
            "post-publisher",
            job => job.PublishPendingPostsAsync(CancellationToken.None),
            "1 * * * *"
        );

        // Statistics collector: Every hour (Cron: "0 * * * *")
        recurringJobManager.AddOrUpdate<StatisticsCollectorJob>(
            "statistics-collector",
            job => job.CollectStatisticsAsync(CancellationToken.None),
            "0 * * * *"
        );
    }
}
