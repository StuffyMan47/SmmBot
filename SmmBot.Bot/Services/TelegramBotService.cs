using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmmBot.Bot.Handlers;
using SmmBot.Bot.States;
using SmmBot.Core.Interfaces.Settings.Models;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SmmBot.Bot.Services;

public class TelegramBotService
{
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<TelegramBotService> _logger;
    private readonly BotConfiguration _config;
    private readonly UserStateCache _stateCache;
    private readonly StartCommandHandler _startCommandHandler;
    private readonly SettingsHandler _settingsHandler;
    private readonly CurrentWeekHandler _currentWeekHandler;
    private readonly NextWeekHandler _nextWeekHandler;
    private readonly NextWeekCallbackHandler _nextWeekCallbackHandler;
    private readonly MediaUploadHandler _mediaUploadHandler;
    private readonly SmmBot.Infrastructure.DAL.DbContext.AppDbContext _dbContext;
    private readonly SmmBot.Core.Interfaces.Ai.IAiService _aiService;

    public TelegramBotService(
        ITelegramBotClient botClient, 
        ILogger<TelegramBotService> logger, 
        IOptions<BotConfiguration> config,
        UserStateCache stateCache,
        StartCommandHandler startCommandHandler,
        SettingsHandler settingsHandler,
        CurrentWeekHandler currentWeekHandler,
        NextWeekHandler nextWeekHandler,
        NextWeekCallbackHandler nextWeekCallbackHandler,
        MediaUploadHandler mediaUploadHandler,
        SmmBot.Infrastructure.DAL.DbContext.AppDbContext dbContext,
        SmmBot.Core.Interfaces.Ai.IAiService aiService)
    {
        _botClient = botClient;
        _logger = logger;
        _config = config.Value;
        _stateCache = stateCache;
        _startCommandHandler = startCommandHandler;
        _settingsHandler = settingsHandler;
        _currentWeekHandler = currentWeekHandler;
        _nextWeekHandler = nextWeekHandler;
        _nextWeekCallbackHandler = nextWeekCallbackHandler;
        _mediaUploadHandler = mediaUploadHandler;
        _dbContext = dbContext;
        _aiService = aiService;
    }

    public async Task HandleUpdateAsync(Update update, CancellationToken cancellationToken)
    {
        var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
        if (chatId == null || !_config.AdminIds.Contains(chatId.Value))
        {
            _logger.LogWarning("Unauthorized access attempt from ChatId: {ChatId}", chatId);
            return;
        }

        try
        {
            if (update.Type == UpdateType.Message && update.Message!.Type == MessageType.Text)
            {
                await HandleTextMessageAsync(update.Message, cancellationToken);
            }
            else if (update.Type == UpdateType.Message && (update.Message!.Type == MessageType.Photo || update.Message!.Type == MessageType.Video))
            {
                var userState = _stateCache.GetState(chatId.Value);
                if (userState.State == BotState.WaitingForPostMedia && userState.Data is long postId)
                {
                    await _mediaUploadHandler.HandleMediaUploadAsync(update.Message, postId, cancellationToken);
                    
                    // We shouldn't clear state immediately if it's a media group, 
                    // because multiple messages will come with the same MediaGroupId.
                    if (string.IsNullOrEmpty(update.Message.MediaGroupId))
                    {
                        _stateCache.ClearState(chatId.Value);
                    }
                    else
                    {
                        // Set a timeout to clear the state or handle it via a background cleanup
                        // For simplicity, we can let the user manually cancel or just keep it
                        // but usually for media groups, we should wait until all are processed.
                        // We will clear it when user sends a text message or clicks a button.
                    }
                }
            }
            else if (update.Type == UpdateType.CallbackQuery)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery!, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update {UpdateId}", update.Id);
            if (chatId.HasValue)
            {
                await _botClient.SendTextMessageAsync(chatId.Value, "Произошла непредвиденная ошибка.", cancellationToken: cancellationToken);
            }
        }
    }

    private async Task HandleTextMessageAsync(Message message, CancellationToken cancellationToken)
    {
        var text = message.Text!;
        var userState = _stateCache.GetState(message.Chat.Id);

        if (text == "/start")
        {
            await _startCommandHandler.HandleAsync(message, cancellationToken);
            return;
        }

        if (userState.State != BotState.None)
        {
            if (userState.State == BotState.WaitingForPostMedia)
            {
                if (text == "/cancel" || text.ToLower() == "отмена" || text == "Готово")
                {
                    _stateCache.ClearState(message.Chat.Id);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Загрузка медиа завершена.", cancellationToken: cancellationToken);
                }
                else
                {
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "Пожалуйста, отправьте фото или нажмите 'Отмена'/'Готово'.", cancellationToken: cancellationToken);
                }
                return;
            }
            if (userState.State == BotState.WaitingForSystemPrompt || userState.State == BotState.WaitingForTargetChannel)
            {
                await _settingsHandler.HandleStateInputAsync(message, userState, cancellationToken);
                return;
            }
            if (userState.State == BotState.WaitingForPostTextEdit && userState.Data is long postIdText)
            {
                var post = await _dbContext.Posts.FindAsync(postIdText, cancellationToken);
                if (post != null)
                {
                    post.Text = message.Text;
                    post.Status = SmmBot.Core.Enums.PostStatus.WaitingForConfirmation;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Текст поста обновлен.", cancellationToken: cancellationToken);
                }
                _stateCache.ClearState(message.Chat.Id);
                return;
            }
            if (userState.State == BotState.WaitingForPostTimeEdit && userState.Data is long postIdTime)
            {
                if (DateTimeOffset.TryParseExact(message.Text, "dd.MM.yyyy HH:mm", null, System.Globalization.DateTimeStyles.AssumeUniversal, out var newTime))
                {
                    var post = await _dbContext.Posts.FindAsync(postIdTime, cancellationToken);
                    if (post != null)
                    {
                        post.ScheduledTime = newTime;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Время публикации поста обновлено.", cancellationToken: cancellationToken);
                    }
                    _stateCache.ClearState(message.Chat.Id);
                }
                else
                {
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "❌ Неверный формат даты и времени. Попробуйте еще раз в формате ДД.ММ.ГГГГ ЧЧ:ММ", cancellationToken: cancellationToken);
                }
                return;
            }
            if (userState.State == BotState.WaitingForPostRegenerationChanges && userState.Data is long postIdRegen)
            {
                await _botClient.SendTextMessageAsync(message.Chat.Id, "Перегенерация поста с учетом ваших правок...", cancellationToken: cancellationToken);
                var post = await _dbContext.Posts.FindAsync(postIdRegen, cancellationToken);
                if (post != null)
                {
                    var updatedJson = await _aiService.EditContentPlanAsync(
                        $"[{{\"Text\": \"{post.Text.Replace("\"", "\\\"")}\", \"ScheduledTime\": \"{post.ScheduledTime:O}\", \"MediaRecommendation\": \"{post.MediaRecommendation?.Replace("\"", "\\\"")}\"}}]", 
                        message.Text, 
                        cancellationToken);
                    
                    var parsedPosts = ParseAiResponse(updatedJson);
                    var regeneratedPost = parsedPosts.FirstOrDefault();
                    
                    if (regeneratedPost != null)
                    {
                        post.Text = regeneratedPost.Text;
                        if (!string.IsNullOrEmpty(regeneratedPost.MediaRecommendation))
                        {
                            post.MediaRecommendation = regeneratedPost.MediaRecommendation;
                        }
                        post.Status = SmmBot.Core.Enums.PostStatus.WaitingForConfirmation;
                        await _dbContext.SaveChangesAsync(cancellationToken);
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Пост перегенерирован.", cancellationToken: cancellationToken);
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(message.Chat.Id, "❌ Не удалось перегенерировать пост. AI вернул неверный формат.", cancellationToken: cancellationToken);
                    }
                }
                _stateCache.ClearState(message.Chat.Id);
                return;
            }
            if (userState.State == BotState.WaitingForContentPlanChanges)
            {
                await _botClient.SendTextMessageAsync(message.Chat.Id, "Генерация нового плана с учетом ваших правок...", cancellationToken: cancellationToken);
                
                var now = DateTimeOffset.UtcNow;
                var currentWeekStart = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).Date;
                var nextWeekStart = currentWeekStart.AddDays(7);
                
                var currentWeekStartUtc = new DateTimeOffset(currentWeekStart, TimeSpan.Zero);
                var nextWeekStartUtc = new DateTimeOffset(nextWeekStart, TimeSpan.Zero);
                
                // Try to find either current week or next week plan to update (depends on which one user clicked)
                var planToUpdate = await _dbContext.ContentPlans.FirstOrDefaultAsync(p => p.WeekStartDate == nextWeekStartUtc, cancellationToken)
                                   ?? await _dbContext.ContentPlans.FirstOrDefaultAsync(p => p.WeekStartDate == currentWeekStartUtc, cancellationToken);
                
                if (planToUpdate != null)
                {
                    var updatedJson = await _aiService.EditContentPlanAsync(planToUpdate.RawAiResponse ?? "", message.Text, cancellationToken);
                    planToUpdate.RawAiResponse = updatedJson;
                    
                    var parsedPosts = ParseAiResponse(updatedJson);
                    if (parsedPosts.Any())
                    {
                        var existingPosts = await _dbContext.Posts.Where(p => p.ContentPlanId == planToUpdate.Id).ToListAsync(cancellationToken);
                        _dbContext.Posts.RemoveRange(existingPosts);

                        foreach (var p in parsedPosts)
                        {
                            var scheduledTimeUtc = p.ScheduledTime.ToUniversalTime();
                            var scheduledTimeNormalized = new DateTimeOffset(scheduledTimeUtc.DateTime, TimeSpan.Zero);
                            
                            // Prevent current week posts from being scheduled in the past
                            if (planToUpdate.WeekStartDate == currentWeekStartUtc && scheduledTimeNormalized < now)
                            {
                                scheduledTimeNormalized = now.AddHours(1);
                            }

                            planToUpdate.Posts.Add(new SmmBot.Infrastructure.DAL.Entites.Post
                            {
                                ContentPlanId = planToUpdate.Id,
                                Text = p.Text,
                                ScheduledTime = scheduledTimeNormalized,
                                MediaRecommendation = p.MediaRecommendation,
                                Status = SmmBot.Core.Enums.PostStatus.WaitingForConfirmation
                            });
                        }
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                    
                    await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ План обновлен. Используйте меню, чтобы посмотреть его.", cancellationToken: cancellationToken);
                }

                _stateCache.ClearState(message.Chat.Id);
                return;
            }
            
            return;
        }

        switch (text)
        {
            case "Настройки":
                await _settingsHandler.HandleSettingsMenuAsync(message, cancellationToken);
                break;
            case "Контент план на текущую неделю":
                await _currentWeekHandler.HandleMenuAsync(message, cancellationToken);
                break;
            case "Контент план на следующую неделю":
                await _nextWeekHandler.HandleMenuAsync(message, cancellationToken);
                break;
            default:
                await _botClient.SendTextMessageAsync(message.Chat.Id, "Неизвестная команда. Пожалуйста, используйте меню.", cancellationToken: cancellationToken);
                break;
        }
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var data = callbackQuery.Data;
        if (string.IsNullOrEmpty(data)) return;

        if (data.StartsWith("settings_"))
        {
            await _settingsHandler.HandleCallbackAsync(callbackQuery, cancellationToken);
        }
        else if (data.StartsWith("edit_post_") || data.StartsWith("post_action_") || data == "view_all_current_week_posts" || data.StartsWith("back_to_plan_") || data == "generate_current_week_plan" || data == "regenerate_current_week_plan" || data == "confirm_next_week_plan" || data == "confirm_current_week_plan")
        {
            await _currentWeekHandler.HandleCallbackAsync(callbackQuery, cancellationToken);
        }
        else if (data.StartsWith("generate_next_week_plan") || data.EndsWith("_next_week_plan") || data == "view_all_next_week_posts")
        {
            await _nextWeekCallbackHandler.HandleCallbackAsync(callbackQuery, cancellationToken);
        }
    }

    private List<PostDto> ParseAiResponse(string json)
    {
        try
        {
            var startIdx = json.IndexOf('[');
            var endIdx = json.LastIndexOf(']');
            if (startIdx >= 0 && endIdx >= 0)
            {
                var cleanJson = json.Substring(startIdx, endIdx - startIdx + 1);
                var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                return System.Text.Json.JsonSerializer.Deserialize<List<PostDto>>(cleanJson, options) ?? new List<PostDto>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse AI response into posts: {Response}", json);
        }
        
        return new List<PostDto>();
    }

    private class PostDto
    {
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset ScheduledTime { get; set; }
        public string? MediaRecommendation { get; set; }
    }
}
