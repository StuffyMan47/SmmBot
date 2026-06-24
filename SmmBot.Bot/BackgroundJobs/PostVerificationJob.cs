using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using SmmBot.Core.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using SmmBot.Core.Interfaces.Settings.Models;

namespace SmmBot.Bot.BackgroundJobs;

public class PostVerificationJob
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PostVerificationJob> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly BotConfiguration _config;

    public PostVerificationJob(AppDbContext dbContext, ILogger<PostVerificationJob> logger, ITelegramBotClient botClient, IOptions<BotConfiguration> config)
    {
        _dbContext = dbContext;
        _logger = logger;
        _botClient = botClient;
        _config = config.Value;
    }

    public async Task SendVerificationRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.AddHours(3);
        var publishThreshold = now.AddMinutes(20);

        var postsToVerify = await _dbContext.Posts
            .Where(p => p.Status == PostStatus.WaitingForConfirmation && 
                        p.ScheduledTime <= publishThreshold && 
                        p.ScheduledTime > now)
            .ToListAsync(cancellationToken);

        foreach (var post in postsToVerify)
        {
            try
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Опубликовать", $"post_action_confirm_{post.Id}") },
                    new[] { InlineKeyboardButton.WithCallbackData("Отменить", $"post_action_cancel_{post.Id}") },
                    new[] { InlineKeyboardButton.WithCallbackData("🔄 Перегенерировать пост", $"post_action_regen_{post.Id}") }
                });

                var text = $"⚠️ Напоминание!\nПост запланирован на {post.ScheduledTime:HH:mm} (через {(post.ScheduledTime - now).Minutes} мин).\nЕсли вы не нажмете Отменить, он будет опубликован автоматически!\n\nТекст:\n{post.Text}";
                
                _logger.LogInformation("Sending verification reminder for Post {PostId}", post.Id);
                
                if (_config.AdminIds != null)
                {
                    foreach (var adminId in _config.AdminIds)
                    {
                        try
                        {
                            await _botClient.SendTextMessageAsync(adminId, text, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Could not send notification to admin {AdminId}", adminId);
                        }
                    }
                }
                
                post.Status = PostStatus.Confirmed;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification reminder for post {PostId}", post.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
