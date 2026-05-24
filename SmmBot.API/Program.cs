using SmmBot.Infrastructure.DAL.DbContext;
using SmmBot.API.Configurations;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;

using SmmBot.Bot;

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Services.AddInfrastructureServices(builder.Configuration);
    builder.Services.AddBotServices(builder.Configuration);
    builder.Services.AddHangfireServices(builder.Configuration);

    builder.Services
        .AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true)
        .AddJsonOptions(options => options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();
    
    var app = builder.Build();

    app.ConfigureHangfireJobs();

    app.MapGet("/bot/status", () => new { status = "Bot is running in polling mode" });

    using var scope = app.Services.CreateScope();
    await using var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.MigrateAsync();

    var commandService = scope.ServiceProvider.GetRequiredService<SmmBot.Bot.Services.Interfaces.IStartCommandService>();
    await commandService.SetCommandsAsync();

    var botClient = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
    var botService = scope.ServiceProvider.GetRequiredService<SmmBot.Bot.Services.TelegramBotService>();
    
    var receiverOptions = new ReceiverOptions
    {
        AllowedUpdates = Array.Empty<UpdateType>(), // получать все типы обновлений
    };
    
    using var cts = new CancellationTokenSource();
    
    _ = Task.Run(async () =>
    {
        try
        {
            Console.WriteLine("Starting bot in polling mode...");
        
            await botClient.ReceiveAsync(
                updateHandler: async (client, update, cancellationToken) =>
                {
                    try
                    {
                        using var innerScope = app.Services.CreateScope();
                        var scopedBotService = innerScope.ServiceProvider.GetRequiredService<SmmBot.Bot.Services.TelegramBotService>();
                        await scopedBotService.HandleUpdateAsync(update, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error handling update: " + ex.Message);
                    }
                },
                pollingErrorHandler: async (client, exception, cancellationToken) =>
                {
                    Console.WriteLine("Polling error occurred: " + exception.Message);
                    await Task.CompletedTask;
                },
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine("Bot polling crashed: " + ex.Message);
            cts.Cancel();
        }
    });

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
catch (Exception ex) when (!ex.GetType().Name.Equals("HostAbortedException", StringComparison.Ordinal))
{
    Console.WriteLine("Приложение не может быть запущено из-за критической ошибки: " + ex.Message);
    throw;
}
finally
{
    Console.WriteLine("Application shutdown...");
}
