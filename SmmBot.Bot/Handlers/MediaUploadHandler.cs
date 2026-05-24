using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using SmmBot.Core.Interfaces.Storage;
using SmmBot.Infrastructure.DAL.Entites;
using SmmBot.Core.Enums;

namespace SmmBot.Bot.Handlers;

public class MediaUploadHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly AppDbContext _dbContext;
    private readonly IS3StorageService _s3Storage;

    public MediaUploadHandler(ITelegramBotClient botClient, AppDbContext dbContext, IS3StorageService s3Storage)
    {
        _botClient = botClient;
        _dbContext = dbContext;
        _s3Storage = s3Storage;
    }

    public async Task HandleMediaUploadAsync(Telegram.Bot.Types.Message message, long postId, CancellationToken cancellationToken)
    {
        string fileId;
        MediaType mediaType;

        if (message.Type == MessageType.Photo)
        {
            fileId = message.Photo!.Last().FileId;
            mediaType = MediaType.Photo;
        }
        else if (message.Type == MessageType.Video)
        {
            fileId = message.Video!.FileId;
            mediaType = MediaType.Video;
        }
        else return;

        var file = await _botClient.GetFileAsync(fileId, cancellationToken);
        var fileStream = new MemoryStream();
        await _botClient.DownloadFileAsync(file.FilePath!, fileStream, cancellationToken);
        fileStream.Position = 0;

        var extension = Path.GetExtension(file.FilePath) ?? ".jpg";
        var fileName = $"{postId}{extension}";
        var contentType = mediaType == MediaType.Photo ? "image/jpeg" : "video/mp4";

        var url = await _s3Storage.UploadFileAsync(fileStream, fileName, contentType, cancellationToken);

        var mediaFile = new MediaFile
        {
            PostId = postId,
            Type = mediaType,
            FilePath = url,
            FileId = fileId
        };

        _dbContext.MediaFiles.Add(mediaFile);
        
        var post = await _dbContext.Posts.FindAsync(postId, cancellationToken);
        if (post != null)
        {
            post.Status = PostStatus.WaitingForConfirmation;
        }
        
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Медиафайл успешно загружен и прикреплен к посту.", cancellationToken: cancellationToken);
    }
}
