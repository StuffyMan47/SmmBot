using Hangfire;
using Max.Bot;
using Max.Bot.Types.Enums;
using Max.Bot.Types.Requests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using SmmBot.Core.Enums;
using SmmBot.Infrastructure.DAL.Entites;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using ParseMode = Telegram.Bot.Types.Enums.ParseMode;

namespace SmmBot.Bot.BackgroundJobs;

public class PostPublisherJob
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<PostPublisherJob> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly MaxClient _maxBotClient;

    public PostPublisherJob(
        AppDbContext dbContext, 
        ILogger<PostPublisherJob> logger, 
        ITelegramBotClient botClient,
        MaxClient maxBotClient)
    {
        _dbContext = dbContext;
        _logger = logger;
        _botClient = botClient;
        _maxBotClient = maxBotClient;
    }

    public async Task PublishPendingPostsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow.AddHours(3);
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        
        if (settings == null || (string.IsNullOrEmpty(settings.TargetChannelId) && string.IsNullOrEmpty(settings.TargetMaxChannelId)))
        {
            _logger.LogWarning("Target channel is not configured.");
            return;
        }

        var postsToPublish = await _dbContext.Posts
            .Include(p => p.MediaFiles)
            .Where(p => (p.Status == PostStatus.Confirmed) && p.ScheduledTime <= now)
            .ToListAsync(cancellationToken);

        foreach (var post in postsToPublish)
        {
            bool postedInTelegram = false;
            bool postedInMax = false;

            try
            {
                if (!string.IsNullOrEmpty(settings.TargetChannelId))
                {
                    await PublishToTelegramAsync(post, settings.TargetChannelId, cancellationToken);
                    postedInTelegram = true;
                }
                
                if (!string.IsNullOrEmpty(settings.TargetMaxChannelId))
                {
                    await PublishToMaxAsync(post, settings.TargetMaxChannelId, cancellationToken);
                    postedInMax = true;
                }

                post.Status = PostStatus.Published;
                _logger.LogInformation("Successfully published post {PostId}", post.Id);
            }
            catch (Exception ex)
            {
                if (postedInMax || postedInTelegram)
                {
                    post.Status = PostStatus.Published;
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                var msg = await _botClient.SendTextMessageAsync(
                    chatId: 714862316,
                    text: $"не получилось отправить пост: tg {postedInTelegram} or max {postedInMax}",
                    parseMode: ParseMode.Html,
                    disableNotification:true,
                    cancellationToken: cancellationToken);
                _logger.LogError(ex, "Failed to publish post {PostId}", post.Id);
                // Optionally mark as failed/draft
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task PublishToTelegramAsync(Post post, string targetChannelId, CancellationToken cancellationToken)
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
                        chatId: targetChannelId,
                        photo: inputFile,
                        caption: post.Text,
                        parseMode: ParseMode.Html,
                        disableNotification:true,
                        cancellationToken: cancellationToken);
                    
                    post.TelegramMessageId = msg.MessageId.ToString();
                }
                else if (media.Type == MediaType.Video)
                {
                    var inputFile = !string.IsNullOrEmpty(media.FileId) ? (InputFile)InputFile.FromFileId(media.FileId) : (InputFile)SmmBot.Bot.Extensions.MediaHelper.GetInputFile(media.FilePath!, "video.mp4");
                    var msg = await _botClient.SendVideoAsync(
                        chatId: targetChannelId,
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
                        var inputFile = !string.IsNullOrEmpty(media.FileId) ? (InputFile)InputFile.FromFileId(media.FileId) : (InputFile)SmmBot.Bot.Extensions.MediaHelper.GetInputFile(media.FilePath!, "video.mp4");
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
                    chatId: targetChannelId,
                    media: mediaGroup,
                    cancellationToken: cancellationToken);
                    
                post.TelegramMessageId = msgs[0].MessageId.ToString();
            }
        }
        else
        {
            var msg = await _botClient.SendTextMessageAsync(
                chatId: targetChannelId,
                text: post.Text,
                parseMode: ParseMode.Html,
                disableNotification:true,
                cancellationToken: cancellationToken);
                
            post.TelegramMessageId = msg.MessageId.ToString();
        }
    }
    
    private async Task PublishToMaxAsync(Post post, string targetChannelId, CancellationToken cancellationToken)
    {
        long.TryParse(targetChannelId, out var targetChannelIdLong);
        
        if (post.MediaFiles.Any())
        {
            if (post.MediaFiles.Count == 1)
            {
                var media = post.MediaFiles.First();
                if (media.Type == MediaType.Photo || media.Type == MediaType.Video)
                {
                    if (!string.IsNullOrEmpty(media.FilePath))
                    {
                        var attachment = await BuildMaxAttachmentAsync(media.FilePath, media.Type == MediaType.Photo ? "image" : "video", cancellationToken);
                        await _maxBotClient.Messages.SendMessageWithAttachmentAsync(
                            attachment,
                            chatId: targetChannelIdLong,
                            text: post.Text,
                            cancellationToken: cancellationToken);
                    }
                }
            }
            else
            {
                // Max bot currently supports sending single attachment per message.
                // Depending on the exact requirements we can either fallback to send one by one or only send the first one.
                // Sending the first media file with the text for now.
                var media = post.MediaFiles.First();
                if (!string.IsNullOrEmpty(media.FilePath))
                {
                    var attachment = await BuildMaxAttachmentAsync(media.FilePath, media.Type == MediaType.Photo ? "image" : "video", cancellationToken);
                    await _maxBotClient.Messages.SendMessageWithAttachmentAsync(
                        attachment,
                        chatId: targetChannelIdLong,
                        text: post.Text,
                        cancellationToken: cancellationToken);
                }
            }
        }
        else
        {
            await _maxBotClient.Messages.SendMessageAsync(
                chatId: targetChannelIdLong,
                text: post.Text,
                cancellationToken: cancellationToken);
        }
    }
    
    private async Task<AttachmentRequest> BuildMaxAttachmentAsync(string filePathOrBase64, string attachmentType, CancellationToken cancellationToken)
    {
        Stream stream;
        string fileName;

        if (filePathOrBase64.StartsWith("data:image") || filePathOrBase64.StartsWith("data:video"))
        {
            var match = System.Text.RegularExpressions.Regex.Match(filePathOrBase64, @"^data:(?<mime>[\w/\-\.]+);(?<encoding>\w+),(?<data>.*)");
            if (match.Success)
            {
                var base64Data = match.Groups["data"].Value;
                var bytes = Convert.FromBase64String(base64Data);
                stream = new MemoryStream(bytes);
                fileName = attachmentType == "video" ? "video.mp4" : "image.png";
            }
            else
            {
                throw new ArgumentException("Invalid base64 string format");
            }
        }
        else if (filePathOrBase64.StartsWith("http://") || filePathOrBase64.StartsWith("https://"))
        {
            var httpClient = new HttpClient();
            stream = await httpClient.GetStreamAsync(filePathOrBase64, cancellationToken);
            fileName = attachmentType == "video" ? "video.mp4" : "image.png";
        }
        else
        {
            stream = System.IO.File.OpenRead(filePathOrBase64);
            fileName = System.IO.Path.GetFileName(filePathOrBase64);
        }

        var uploadType = attachmentType == "image" ? UploadType.Image : (attachmentType == "video" ? UploadType.Video : UploadType.File);
        var uploadResponse = await _maxBotClient.Files.UploadFileAsync(uploadType, cancellationToken).ConfigureAwait(false);
        var payload = await _maxBotClient.Files.UploadFileDataAsync(
            uploadResponse.Url,
            stream,
            fileName,
            cancellationToken).ConfigureAwait(false);

        await stream.DisposeAsync();

        return new AttachmentRequest
        {
            Type = attachmentType,
            Payload = payload
        };
    }
}
