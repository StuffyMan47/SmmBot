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
        await HandleSettingsMenuAsync(message.Chat.Id, cancellationToken);
    }

    public async Task HandleSettingsMenuAsync(long chatId, CancellationToken cancellationToken)
    {
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        
        var systemPromptStatus = string.IsNullOrEmpty(settings?.SystemPrompt) ? "Не задан" : "Задан";
        var channelStatus = string.IsNullOrEmpty(settings?.TargetChannelId) ? "Не задан" : settings.TargetChannelId;
        var maxChannelStatus = string.IsNullOrEmpty(settings?.TargetMaxChannelId) ? "Не задан" : settings.TargetMaxChannelId;
        var textModelStatus = string.IsNullOrEmpty(settings?.TextModel) ? "qwen/qwen3.6-plus" : settings.TextModel;
        var imageModelStatus = string.IsNullOrEmpty(settings?.ImageModel) ? "google/gemini-3.1-flash-image-preview" : settings.ImageModel;
        var videoModelStatus = string.IsNullOrEmpty(settings?.VideoModel) ? "x-ai/grok-imagine-video" : settings.VideoModel;

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData($"Системный промпт ({systemPromptStatus})", "settings_system_prompt") },
            new[] { InlineKeyboardButton.WithCallbackData($"Канал для публикации ({channelStatus})", "settings_target_channel") },
            new[] { InlineKeyboardButton.WithCallbackData($"Канал Max ({maxChannelStatus})", "settings_target_max_channel") },
            new[] { InlineKeyboardButton.WithCallbackData($"Текстовая модель ({textModelStatus})", "settings_text_model") },
            new[] { InlineKeyboardButton.WithCallbackData($"Фото модель ({imageModelStatus})", "settings_image_model") },
            new[] { InlineKeyboardButton.WithCallbackData($"Видео модель ({videoModelStatus})", "settings_video_model") }
        });

        await _botClient.SendTextMessageAsync(
            chatId: chatId,
            text: "⚙️ Настройки бота\nВыберите параметр для изменения:",
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken
        );
    }

    public async Task HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;

        var cancelKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("❌ Отмена", "settings_cancel_input") }
        });

        if (callbackQuery.Data == "settings_cancel_input")
        {
            _stateCache.ClearState(chatId);
            await _botClient.SendTextMessageAsync(chatId, "Ввод отменен.", cancellationToken: cancellationToken);
            await HandleSettingsMenuAsync(chatId, cancellationToken);
        }
        else if (callbackQuery.Data == "settings_system_prompt")
        {
            var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
            var text = "📝 Отправьте новый системный промпт.";
            if (!string.IsNullOrEmpty(settings?.SystemPrompt))
            {
                text = $"Текущий промпт:\n\n{settings.SystemPrompt}\n\nОтправьте новый системный промпт, чтобы перезаписать его.";
            }

            _stateCache.SetState(chatId, BotState.WaitingForSystemPrompt);
            await _botClient.SendTextMessageAsync(chatId, text, replyMarkup: cancelKeyboard, cancellationToken: cancellationToken);
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
            await _botClient.SendTextMessageAsync(chatId, text, replyMarkup: cancelKeyboard, cancellationToken: cancellationToken);
        }
        else if (callbackQuery.Data == "settings_target_max_channel")
        {
            var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
            var text = "📢 Отправьте ID канала Max, куда бот будет выкладывать посты.";
            if (!string.IsNullOrEmpty(settings?.TargetMaxChannelId))
            {
                text = $"Текущий канал Max: {settings.TargetMaxChannelId}\n\nОтправьте новый ID канала Max.";
            }

            _stateCache.SetState(chatId, BotState.WaitingForTargetMaxChannel);
            await _botClient.SendTextMessageAsync(chatId, text, replyMarkup: cancelKeyboard, cancellationToken: cancellationToken);
        }
        else if (callbackQuery.Data == "settings_text_model")
        {
            var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
            var currentModel = string.IsNullOrEmpty(settings?.TextModel) ? "qwen/qwen3.6-plus" : settings.TextModel;
            var text = $"🤖 Текущая текстовая модель: {currentModel}\n\nОтправьте название новой текстовой модели.";

            _stateCache.SetState(chatId, BotState.WaitingForTextModel);
            await _botClient.SendTextMessageAsync(chatId, text, replyMarkup: cancelKeyboard, cancellationToken: cancellationToken);
        }
        else if (callbackQuery.Data == "settings_image_model")
        {
            var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
            var currentModel = string.IsNullOrEmpty(settings?.ImageModel) ? "google/gemini-3.1-flash-image-preview" : settings.ImageModel;
            var text = $"🖼 Текущая фото модель: {currentModel}\n\nОтправьте название новой фото модели.";

            _stateCache.SetState(chatId, BotState.WaitingForImageModel);
            await _botClient.SendTextMessageAsync(chatId, text, replyMarkup: cancelKeyboard, cancellationToken: cancellationToken);
        }
        else if (callbackQuery.Data == "settings_video_model")
        {
            var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
            var currentModel = string.IsNullOrEmpty(settings?.VideoModel) ? "x-ai/grok-imagine-video" : settings.VideoModel;
            var text = $"🎥 Текущая видео модель: {currentModel}\n\nОтправьте название новой видео модели.";

            _stateCache.SetState(chatId, BotState.WaitingForVideoModel);
            await _botClient.SendTextMessageAsync(chatId, text, replyMarkup: cancelKeyboard, cancellationToken: cancellationToken);
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
        else if (userState.State == BotState.WaitingForTargetMaxChannel)
        {
            settings.TargetMaxChannelId = message.Text;
            await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Канал Max для публикации успешно сохранен.", cancellationToken: cancellationToken);
        }
        else if (userState.State == BotState.WaitingForTextModel)
        {
            settings.TextModel = message.Text;
            await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Текстовая модель успешно сохранена.", cancellationToken: cancellationToken);
        }
        else if (userState.State == BotState.WaitingForImageModel)
        {
            settings.ImageModel = message.Text;
            await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Фото модель успешно сохранена.", cancellationToken: cancellationToken);
        }
        else if (userState.State == BotState.WaitingForVideoModel)
        {
            settings.VideoModel = message.Text;
            await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Видео модель успешно сохранена.", cancellationToken: cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _stateCache.ClearState(message.Chat.Id);
        
        await HandleSettingsMenuAsync(message, cancellationToken);
    }
}
