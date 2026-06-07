using Microsoft.EntityFrameworkCore;
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
        if (message.Type != MessageType.Photo && message.Type != MessageType.Video)
        {
            await _botClient.SendTextMessageAsync(message.Chat.Id, "❌ Ошибка: поддерживаются только изображения и видео.", cancellationToken: cancellationToken);
            return;
        }

        string fileId;
        MediaType mediaType;

        if (message.Type == MessageType.Photo)
        {
            fileId = message.Photo!.Last().FileId;
            mediaType = MediaType.Photo;
            
            // For photos, we keep the Base64 logic as requested
            var file = await _botClient.GetFileAsync(fileId, cancellationToken);
            var fileStream = new MemoryStream();
            await _botClient.DownloadFileAsync(file.FilePath!, fileStream, cancellationToken);
            fileStream.Position = 0;
            
            var bytes = fileStream.ToArray();
            var base64 = Convert.ToBase64String(bytes);
            var filePath = $"data:image/jpeg;base64,{base64}";

            var existMediaPhoto = await _dbContext.MediaFiles.Where(x=>x.PostId == postId && x.FilePath == fileId).ToListAsync(cancellationToken);
            if (existMediaPhoto.Any())
            {
                return;
            }

            var mediaFilePhoto = new MediaFile
            {
                PostId = postId,
                Type = MediaType.Photo,
                FilePath = filePath,
                FileId = fileId
            };
            
            _dbContext.MediaFiles.Add(mediaFilePhoto);
        }
        else
        {
            // For videos, use SeaweedFS via S3
            fileId = message.Video!.FileId;
            mediaType = MediaType.Video;
            var ext = !string.IsNullOrEmpty(message.Video.MimeType) && message.Video.MimeType.Contains("/") ? 
                message.Video.MimeType.Split('/')[1] : "mp4";
            var fileName = $"{fileId}.{ext}";
            var contentType = message.Video.MimeType ?? "video/mp4";

            var existMediaVideo = await _dbContext.MediaFiles.Where(x=>x.PostId == postId && x.FileId == fileId).ToListAsync(cancellationToken);
            if (existMediaVideo.Any())
            {
                return;
            }

            var file = await _botClient.GetFileAsync(fileId, cancellationToken);
            var fileStream = new MemoryStream();
            await _botClient.DownloadFileAsync(file.FilePath!, fileStream, cancellationToken);
            fileStream.Position = 0;
            
            string fileUrl = await _s3Storage.UploadFileAsync(fileStream, fileName, contentType, cancellationToken);

            var mediaFileVideo = new MediaFile
            {
                PostId = postId,
                Type = MediaType.Video,
                FilePath = fileUrl,
                FileId = fileId
            };

            _dbContext.MediaFiles.Add(mediaFileVideo);
        }
        
        var post = await _dbContext.Posts.FindAsync(postId, cancellationToken);
        if (post != null)
        {
            post.Status = PostStatus.WaitingForConfirmation;
        }
        
        await _dbContext.SaveChangesAsync(cancellationToken);

        string successMessage = mediaType == MediaType.Photo 
            ? "✅ Изображение успешно загружено и прикреплено к посту в формате Base64." 
            : "✅ Видео успешно загружено и прикреплено к посту.";
        await _botClient.SendTextMessageAsync(message.Chat.Id, successMessage, cancellationToken: cancellationToken);
    }
}
