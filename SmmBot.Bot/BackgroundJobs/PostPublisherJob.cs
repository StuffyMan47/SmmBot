using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using SmmBot.Core.Enums;
using SmmBot.Infrastructure.DAL.Entites;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace SmmBot.Bot.BackgroundJobs;

public class PostPublisherJob
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PostPublisherJob> _logger;
    private readonly ITelegramBotClient _botClient;

    public PostPublisherJob(AppDbContext dbContext, ILogger<PostPublisherJob> logger, ITelegramBotClient botClient)
    {
        _dbContext = dbContext;
        _logger = logger;
        _botClient = botClient;
    }

    public async Task PublishPendingPostsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.AddHours(3);
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        
        if (settings == null || string.IsNullOrEmpty(settings.TargetChannelId))
        {
            _logger.LogWarning("Target channel is not configured.");
            return;
        }

        var postsToPublish = await _dbContext.Posts
            .Include(p => p.MediaFiles)
            .Where(p => p.Status == PostStatus.Confirmed && p.ScheduledTime <= now)
            .ToListAsync(cancellationToken);

        foreach (var post in postsToPublish)
        {
            try
            {
                if (post.MediaFiles.Any())
                {
                    // Logic to upload media - simplified for text only for now or single photo
                    var media = post.MediaFiles.First();
                    if (media.Type == MediaType.Photo)
                    {
                        var inputFile = !string.IsNullOrEmpty(media.FileId) ? InputFile.FromFileId(media.FileId) : SmmBot.Bot.Extensions.MediaHelper.GetInputFile(media.FilePath!);
                        var msg = await _botClient.SendPhotoAsync(
                            chatId: settings.TargetChannelId,
                            photo: inputFile,
                            caption: post.Text,
                            parseMode: ParseMode.Html,
                            cancellationToken: cancellationToken);
                        
                        post.TelegramMessageId = msg.MessageId.ToString();
                    }
                }
                else
                {
                    var msg = await _botClient.SendTextMessageAsync(
                        chatId: settings.TargetChannelId,
                        text: post.Text,
                        parseMode: ParseMode.Html,
                        cancellationToken: cancellationToken);
                        
                    post.TelegramMessageId = msg.MessageId.ToString();
                }

                post.Status = PostStatus.Published;
                _logger.LogInformation("Successfully published post {PostId}", post.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish post {PostId}", post.Id);
                // Optionally mark as failed/draft
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
