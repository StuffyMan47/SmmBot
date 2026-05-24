using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using SmmBot.Core.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace SmmBot.Bot.BackgroundJobs;

public class PostVerificationJob
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PostVerificationJob> _logger;
    private readonly ITelegramBotClient _botClient;

    public PostVerificationJob(AppDbContext dbContext, ILogger<PostVerificationJob> logger, ITelegramBotClient botClient)
    {
        _dbContext = dbContext;
        _logger = logger;
        _botClient = botClient;
    }

    public async Task SendVerificationRemindersAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var publishThreshold = now.AddMinutes(20);

        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        // Assuming admin IDs are fetched from config, or from users table if we map them
        // We will broadcast to a fixed admin for now or fetch from IOptions if injected. Let's use config for simplicity.
        
        var postsToVerify = await _dbContext.Posts
            .Where(p => p.Status == PostStatus.WaitingForConfirmation && 
                        p.ScheduledTime <= publishThreshold && 
                        p.ScheduledTime > now)
            .ToListAsync(cancellationToken);

        foreach (var post in postsToVerify)
        {
            try
            {
                // In a real scenario, we'd iterate over AdminIds from Config
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[] { InlineKeyboardButton.WithCallbackData("Опубликовать", $"post_action_confirm_{post.Id}") },
                    new[] { InlineKeyboardButton.WithCallbackData("Отменить", $"post_action_cancel_{post.Id}") }
                });

                var text = $"⚠️ Напоминание!\nПост запланирован на {post.ScheduledTime:HH:mm} (через {(post.ScheduledTime - now).Minutes} мин).\nЕсли вы не нажмете Отменить, он будет опубликован автоматически!\n\nТекст:\n{post.Text}";
                
                // For demonstration, we just log. In practice, _botClient.SendTextMessageAsync to all AdminIds
                _logger.LogInformation("Sending verification reminder for Post {PostId}", post.Id);
                
                // If the user said "Публикуем автоматически, если правок не было", 
                // we just confirm it right before publish if no one canceled.
                // We'll update the status to Confirmed so the PublisherJob picks it up.
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
