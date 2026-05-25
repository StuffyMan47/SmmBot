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
        if (message.Type != MessageType.Photo)
        {
            await _botClient.SendTextMessageAsync(message.Chat.Id, "❌ Ошибка: поддерживаются только изображения.", cancellationToken: cancellationToken);
            return;
        }

        string fileId = message.Photo!.Last().FileId;

        var file = await _botClient.GetFileAsync(fileId, cancellationToken);
        var fileStream = new MemoryStream();
        await _botClient.DownloadFileAsync(file.FilePath!, fileStream, cancellationToken);
        fileStream.Position = 0;
        
        var bytes = fileStream.ToArray();
        var base64 = Convert.ToBase64String(bytes);
        var base64Url = $"data:image/jpeg;base64,{base64}";

        var mediaFile = new MediaFile
        {
            PostId = postId,
            Type = MediaType.Photo,
            FilePath = base64Url,
            FileId = null // FileId is not needed since we store base64 directly
        };

        _dbContext.MediaFiles.Add(mediaFile);
        
        var post = await _dbContext.Posts.FindAsync(postId, cancellationToken);
        if (post != null)
        {
            post.Status = PostStatus.WaitingForConfirmation;
        }
        
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _botClient.SendTextMessageAsync(message.Chat.Id, "✅ Изображение успешно загружено и прикреплено к посту в формате Base64.", cancellationToken: cancellationToken);
    }
}
