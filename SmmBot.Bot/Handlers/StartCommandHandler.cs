using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using SmmBot.Bot.States;

namespace SmmBot.Bot.Handlers;

public class StartCommandHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly UserStateCache _stateCache;

    public StartCommandHandler(ITelegramBotClient botClient, UserStateCache stateCache)
    {
        _botClient = botClient;
        _stateCache = stateCache;
    }

    public async Task HandleAsync(Message message, CancellationToken cancellationToken)
    {
        _stateCache.ClearState(message.Chat.Id);

        var keyboard = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "Контент план на текущую неделю" },
            new KeyboardButton[] { "Контент план на следующую неделю" },
            new KeyboardButton[] { "Настройки" }
        })
        {
            ResizeKeyboard = true
        };

        await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "Добро пожаловать в панель управления SmmBot!\nВыберите нужное действие в меню ниже.",
            replyMarkup: keyboard,
            cancellationToken: cancellationToken
        );
    }
}
