using Microsoft.EntityFrameworkCore;
using SmmBot.Bot.States;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using SmmBot.Core.Entities;

namespace SmmBot.Bot.Handlers;

public class SettingsHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly UserStateCache _stateCache;
    private readonly AppDbContext _dbContext;

    public SettingsHandler(ITelegramBotClient botClient, UserStateCache stateCache, AppDbContext dbContext)
    {
        _botClient = botClient;
        _stateCache = stateCache;
        _dbContext = dbContext;
    }

    public async Task HandleSettingsMenuAsync(Message message, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        
        var systemPromptStatus = string.IsNullOrEmpty(settings?.SystemPrompt) ? "Не задан" : "Задан";
        var channelStatus = string.IsNullOrEmpty(settings?.TargetChannelId) ? "Не задан" : settings.TargetChannelId;

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData($"Системный промпт ({systemPromptStatus})", "settings_system_prompt") },
            new[] { InlineKeyboardButton.WithCallbackData($"Канал для публикации ({channelStatus})", "settings_target_channel") }
        });

        await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: "⚙️ Настройки бота\nВыберите параметр для изменения:",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken
        );
    }

    public async Task HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;

        if (callbackQuery.Data == "settings_system_prompt")
        {
            var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
            var text = "📝 Отправьте новый системный промпт.";
            if (!string.IsNullOrEmpty(settings?.SystemPrompt))
            {
                text = $"Текущий промпт:\n\n{settings.SystemPrompt}\n\nОтправьте новый системный промпт, чтобы перезаписать его.";
            }

            _stateCache.SetState(chatId, BotState.WaitingForSystemPrompt);
            await _botClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken);
        }
        else if (callbackQuery.Data == "settings_target_channel")
        {
            var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
            var text = "📢 Отправьте ID канала или username (например, @mychannel), куда бот будет выкладывать посты.";
            if (!string.IsNullOrEmpty(settings?.TargetChannelId))
            {
                text = $"Текущий канал: {settings.TargetChannelId}\n\nОтправьте новый ID или username канала.";
            }

            _stateCache.SetState(chatId, BotState.WaitingForTargetChannel);
            await _botClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken);
        }

        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    public async Task HandleStateInputAsync(Message message, UserStateData userState, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null)
        {
            settings = new BotSettings();
            _dbContext.BotSettings.Add(settings);
        }

        if (userState.State == BotState.WaitingForSystemPrompt)
        {
            settings.SystemPrompt = message.Text;
            await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Системный промпт успешно сохранен.", cancellationToken: cancellationToken);
        }
        else if (userState.State == BotState.WaitingForTargetChannel)
        {
            settings.TargetChannelId = message.Text;
            await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Канал для публикации успешно сохранен. Убедитесь, что бот добавлен в этот канал как администратор.", cancellationToken: cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _stateCache.ClearState(message.Chat.Id);
        
        await HandleSettingsMenuAsync(message, cancellationToken);
    }
}
