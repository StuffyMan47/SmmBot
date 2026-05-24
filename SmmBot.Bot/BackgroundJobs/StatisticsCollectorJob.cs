using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using SmmBot.Core.Enums;
using SmmBot.Infrastructure.DAL.Entites;

namespace SmmBot.Bot.BackgroundJobs;

public class StatisticsCollectorJob
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<StatisticsCollectorJob> _logger;
    private readonly ITelegramBotClient _botClient;

    public StatisticsCollectorJob(AppDbContext dbContext, ILogger<StatisticsCollectorJob> logger, ITelegramBotClient botClient)
    {
        _dbContext = dbContext;
        _logger = logger;
        _botClient = botClient;
    }

    public async Task CollectStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null || string.IsNullOrEmpty(settings.TargetChannelId)) return;

        var publishedPosts = await _dbContext.Posts
            .Include(p => p.Statistics)
            .Where(p => p.Status == PostStatus.Published && p.TelegramMessageId != null)
            .OrderByDescending(p => p.ScheduledTime)
            .Take(50) // Update stats for last 50 posts
            .ToListAsync(cancellationToken);

        foreach (var post in publishedPosts)
        {
            try
            {
                // In a real application, you would use mtproto or alternative to get views/reactions 
                // since standard Bot API has limited methods for getting views. 
                // Alternatively, forward message to a private chat and read views.
                // For this prototype, we'll mock the updates.

                if (post.Statistics == null)
                {
                    post.Statistics = new PostStatistics { PostId = post.Id };
                    _dbContext.PostStatistics.Add(post.Statistics);
                }

                // Mock stats increment
                post.Statistics.Views += new Random().Next(10, 50);
                post.Statistics.Reactions += new Random().Next(0, 5);
                post.Statistics.LastUpdated = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect stats for post {PostId}", post.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
