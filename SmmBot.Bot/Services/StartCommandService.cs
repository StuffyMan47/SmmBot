using SmmBot.Bot.Services.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace SmmBot.Bot.Services;

public class StartCommandService : IStartCommandService
{
    private readonly ITelegramBotClient _botClient;

    public StartCommandService(ITelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    public async Task SetCommandsAsync()
    {
        var commands = new[]
        {
            new BotCommand { Command = "start", Description = "Главное меню" }
        };

        await _botClient.SetMyCommandsAsync(commands);
    }
}
