using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using SmmBot.Core.Enums;
using SmmBot.Core.Interfaces.Ai;
using SmmBot.Core.Interfaces.Settings.Models;
using SmmBot.Infrastructure.DAL.Entites;
using SmmBot.Core.Interfaces.Storage;
using System.Net.Http;

namespace SmmBot.Bot.BackgroundJobs;

public class VideoGenerationJob
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<VideoGenerationJob> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly IAiService _aiService;
    private readonly BotConfiguration _config;
    private readonly IS3StorageService _s3StorageService;
    private readonly HttpClient _httpClient;

    public VideoGenerationJob(AppDbContext dbContext, ILogger<VideoGenerationJob> logger, ITelegramBotClient botClient, IAiService aiService, IOptions<BotConfiguration> config, IS3StorageService s3StorageService, HttpClient httpClient)
    {
        _dbContext = dbContext;
        _logger = logger;
        _botClient = botClient;
        _aiService = aiService;
        _config = config.Value;
        _s3StorageService = s3StorageService;
        _httpClient = httpClient;
    }

    public async Task GenerateVideoForPostAsync(long postId, CancellationToken cancellationToken = default)
    {
        var post = await _dbContext.Posts.Include(p => p.ContentPlan).FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null) return;
        
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null || string.IsNullOrEmpty(settings.SystemPrompt)) return;

        try
        {
            var videoUrl = await _aiService.GenerateVideoAsync(post.MediaRecommendation ?? post.Text, cancellationToken);

            if (!string.IsNullOrEmpty(videoUrl))
            {
                var request = new HttpRequestMessage(HttpMethod.Get, videoUrl);
                request.Headers.Add("Authorization", $"Bearer {_config.AiToken}"); // Added auth header
                var response = await _httpClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                var videoStream = await response.Content.ReadAsStreamAsync(cancellationToken);

                var fileName = $"video_post_{post.Id}.mp4";
                var s3Url = await _s3StorageService.UploadFileAsync(videoStream, fileName, "video/mp4", cancellationToken);

                var mediaFile = new MediaFile
                {
                    PostId = postId,
                    Type = MediaType.Video,
                    FilePath = s3Url, // s3Url is stored, ensuring we reference SeaweedFS, not original AI API URL
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
                            text: $"✅ Видео для поста на {post.ScheduledTime:dd.MM.yyyy HH:mm} сгенерировано.", 
                            replyMarkup: inlineKeyboard,
                            cancellationToken: cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not send notification to admin {AdminId}", adminId);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            foreach (var adminId in _config.AdminIds)
            {
                await _botClient.SendTextMessageAsync(adminId, $"Ошибка в процессе генерации видео: {ex.Message}",
                    cancellationToken: cancellationToken);
            }
            _logger.LogError(ex, "Failed to generate video for post {PostId}", postId);
        }
    }
}