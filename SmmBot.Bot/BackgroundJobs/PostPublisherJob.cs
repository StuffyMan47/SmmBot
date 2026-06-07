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
                    if (post.MediaFiles.Count == 1)
                    {
                        var media = post.MediaFiles.First();
                        if (media.Type == MediaType.Photo)
                        {
                            var inputFile = !string.IsNullOrEmpty(media.FileId) ? (InputFile)InputFile.FromFileId(media.FileId) : (InputFile)SmmBot.Bot.Extensions.MediaHelper.GetInputFile(media.FilePath!);
                            var msg = await _botClient.SendPhotoAsync(
                                chatId: settings.TargetChannelId,
                                photo: inputFile,
                                caption: post.Text,
                                parseMode: ParseMode.Html,
                                disableNotification:true,
                                cancellationToken: cancellationToken);
                            
                            post.TelegramMessageId = msg.MessageId.ToString();
                        }
                        else if (media.Type == MediaType.Video)
                        {
                            var inputFile = !string.IsNullOrEmpty(media.FileId) ? (InputFile)InputFile.FromFileId(media.FileId) : (InputFile)InputFile.FromUri(media.FilePath!);
                            var msg = await _botClient.SendVideoAsync(
                                chatId: settings.TargetChannelId,
                                video: inputFile,
                                caption: post.Text,
                                parseMode: ParseMode.Html,
                                disableNotification: true,
                                cancellationToken: cancellationToken);
                            
                            post.TelegramMessageId = msg.MessageId.ToString();
                        }
                    }
                    else
                    {
                        var mediaGroup = new List<IAlbumInputMedia>();
                        foreach (var media in post.MediaFiles)
                        {
                            if (media.Type == MediaType.Photo)
                            {
                                var inputFile = !string.IsNullOrEmpty(media.FileId) ? (InputFile)InputFile.FromFileId(media.FileId) : (InputFile)SmmBot.Bot.Extensions.MediaHelper.GetInputFile(media.FilePath!);
                                var inputMedia = new InputMediaPhoto(inputFile);
                                if (mediaGroup.Count == 0 && !string.IsNullOrEmpty(post.Text))
                                {
                                    inputMedia.Caption = post.Text;
                                    inputMedia.ParseMode = ParseMode.Html;
                                }
                                mediaGroup.Add(inputMedia);
                            }
                            else if (media.Type == MediaType.Video)
                            {
                                var inputFile = !string.IsNullOrEmpty(media.FileId) ? (InputFile)InputFile.FromFileId(media.FileId) : (InputFile)InputFile.FromUri(media.FilePath!);
                                var inputMedia = new InputMediaVideo(inputFile);
                                if (mediaGroup.Count == 0 && !string.IsNullOrEmpty(post.Text))
                                {
                                    inputMedia.Caption = post.Text;
                                    inputMedia.ParseMode = ParseMode.Html;
                                }
                                mediaGroup.Add(inputMedia);
                            }
                        }

                        var msgs = await _botClient.SendMediaGroupAsync(
                            chatId: settings.TargetChannelId,
                            media: mediaGroup,
                            cancellationToken: cancellationToken);
                            
                        post.TelegramMessageId = msgs[0].MessageId.ToString();
                    }
                }
                else
                {
                    var msg = await _botClient.SendTextMessageAsync(
                        chatId: settings.TargetChannelId,
                        text: post.Text,
                        parseMode: ParseMode.Html,
                        disableNotification:true,
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
