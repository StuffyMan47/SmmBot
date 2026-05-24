using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;
using SmmBot.Core.Enums;
using SmmBot.Core.Interfaces.Ai;
using SmmBot.Core.Interfaces.Settings.Models;
using SmmBot.Infrastructure.DAL.Entites;

namespace SmmBot.Bot.BackgroundJobs;

public class ImageGenerationJob
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ImageGenerationJob> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly IAiService _aiService;
    private readonly BotConfiguration _config;

    public ImageGenerationJob(AppDbContext dbContext, ILogger<ImageGenerationJob> logger, ITelegramBotClient botClient, IAiService aiService, IOptions<BotConfiguration> config)
    {
        _dbContext = dbContext;
        _logger = logger;
        _botClient = botClient;
        _aiService = aiService;
        _config = config.Value;
    }

    public async Task GenerateImageForPostAsync(long postId, CancellationToken cancellationToken = default)
    {
        var post = await _dbContext.Posts.Include(p => p.ContentPlan).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null) return;
        
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null || string.IsNullOrEmpty(settings.SystemPrompt)) return;

        try
        {
            var imagePrompt = await _aiService.GenerateImagePromptAsync(post.Text, cancellationToken);
            var imageUrl = await _aiService.GenerateImageAsync(imagePrompt, cancellationToken);

            if (!string.IsNullOrEmpty(imageUrl))
            {
                var mediaFile = new MediaFile
                {
                    PostId = postId,
                    Type = MediaType.Photo,
                    FilePath = imageUrl,
                    FileId = null
                };

                _dbContext.MediaFiles.Add(mediaFile);
                post.Status = PostStatus.WaitingForConfirmation;
                
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                foreach (var adminId in _config.AdminIds)
                {
                    try
                    {
                        var inlineKeyboard = new InlineKeyboardMarkup(new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("Посмотреть", $"edit_post_{post.Id}") }
                        });

                        await _botClient.SendTextMessageAsync(
                            chatId: adminId, 
                            text: $"✅ Изображение для поста на {post.ScheduledTime:dd.MM.yyyy HH:mm} сгенерировано.", 
                            replyMarkup: inlineKeyboard,
                            cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not send notification to admin {AdminId}", adminId);
                    }
                }
                // Notify admins
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    // send notification to admins
                }
            }
        }
        catch (Exception ex)
        {
            foreach (var adminId in _config.AdminIds)
            {
                await _botClient.SendTextMessageAsync(adminId, $"Ошибка в процессе генерации изображения: {ex.Message}",
                    cancellationToken: cancellationToken);
            }
            _logger.LogError(ex, "Failed to generate image for post {PostId}", postId);
        }
    }
}
