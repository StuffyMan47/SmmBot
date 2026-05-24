using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmmBot.Bot.Handlers;
using SmmBot.Bot.Services;
using SmmBot.Bot.Services.Interfaces;
using SmmBot.Bot.BackgroundJobs;
using SmmBot.Bot.States;
using Telegram.Bot;

namespace SmmBot.Bot;

public static class DependencyInjection
{
    public static IServiceCollection AddBotServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<ITelegramBotClient>(sp =>
        {
            var token = configuration["BotConfiguration:Token"]!;
            return new TelegramBotClient(token);
        });

        services.AddSingleton<UserStateCache>();
        
        services.AddScoped<IStartCommandService, StartCommandService>();
        services.AddScoped<TelegramBotService>();
        
        services.AddScoped<StartCommandHandler>();
        services.AddScoped<SettingsHandler>();
        services.AddScoped<CurrentWeekHandler>();
        services.AddScoped<NextWeekHandler>();
        services.AddScoped<NextWeekCallbackHandler>();
        services.AddScoped<MediaUploadHandler>();

        services.AddScoped<ContentPlanGenerationJob>();
        services.AddScoped<PostPublisherJob>();
        services.AddScoped<PostVerificationJob>();
        services.AddScoped<StatisticsCollectorJob>();
        services.AddScoped<ImageGenerationJob>();

        return services;
    }
}
