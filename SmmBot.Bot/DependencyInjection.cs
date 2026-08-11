using Max.Bot;
using Max.Bot.Configuration;
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
        
        var maxBotToken = configuration.GetSection("BotConfiguration").GetSection("MaxToken");
        services.AddSingleton<MaxClient>(provider =>
        {
            return new MaxClient(new MaxBotOptions
            {
                Token = maxBotToken.Value,
                // BaseUrl = "https://platform-api2.max.ru/"
            });
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
        services.AddScoped<VideoGenerationJob>();
        
        return services;
    }
}
