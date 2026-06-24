using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmmBot.Core.Interfaces.Ai;
using SmmBot.Infrastructure.DAL.DbContext;
using SmmBot.Infrastructure.DAL.Entites;
using Telegram.Bot;
using SmmBot.Core.Enums;
using System.Text.Json;
using SmmBot.Core.Interfaces.Settings.Models;

namespace SmmBot.Bot.BackgroundJobs;

public class ContentPlanGenerationJob
{
    private readonly IAiService _aiService;
    private readonly AppDbContext _dbContext;
    private readonly ILogger<ContentPlanGenerationJob> _logger;
    private readonly ITelegramBotClient _botClient;
    private readonly BotConfiguration _config;

    public ContentPlanGenerationJob(IAiService aiService, AppDbContext dbContext, ILogger<ContentPlanGenerationJob> logger, ITelegramBotClient botClient, IOptions<BotConfiguration> config)
    {
        _aiService = aiService;
        _dbContext = dbContext;
        _logger = logger;
        _botClient = botClient;
        _config = config.Value;
    }

    public async Task GeneratePlanForCurrentWeekAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting content plan generation for current week...");
        
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null || string.IsNullOrEmpty(settings.SystemPrompt))
        {
            _logger.LogWarning("System prompt is not configured. Cannot generate plan.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).Date;
        var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);
        
        var startOfWeekUtc = new DateTimeOffset(startOfWeek, TimeSpan.Zero);
        var endOfWeekUtc = new DateTimeOffset(endOfWeek, TimeSpan.Zero);

        var existingPlan = await _dbContext.ContentPlans
            .FirstOrDefaultAsync(p => p.WeekStartDate == startOfWeekUtc, cancellationToken);
            
        if (existingPlan != null)
        {
            _logger.LogInformation("Plan for current week already exists.");
            return;
        }

        var previousPlans = await _dbContext.ContentPlans
            .OrderByDescending(p => p.WeekStartDate)
            .Take(4)
            .Select(p => p.RawAiResponse)
            .ToListAsync(cancellationToken);

        var previousPlansText = string.Join("\n---\n", previousPlans.Where(p => p != null));
        var stats = await GetRecentStatisticsAsync(cancellationToken);

        try
        {
            string aiResponse = string.Empty;
            List<PostDto> parsedPosts = new List<PostDto>();
            int retryCount = 0;
            const int maxRetries = 3;
            bool success = false;

            while (retryCount < maxRetries && !success)
            {
                aiResponse = await _aiService.GenerateContentPlanAsync(settings.SystemPrompt, previousPlansText, stats, startOfWeekUtc, cancellationToken);
                
                try
                {
                    parsedPosts = ParseAiResponse(aiResponse);
                    if (parsedPosts.Any())
                    {
                        success = true;
                    }
                    else
                    {
                        _logger.LogWarning("Parsed posts list is empty. Retrying... ({RetryCount}/{MaxRetries})", retryCount + 1, maxRetries);
                        retryCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse AI response. Retrying... ({RetryCount}/{MaxRetries})", retryCount + 1, maxRetries);
                    retryCount++;
                }
            }

            if (!success)
            {
                _logger.LogError("Failed to generate and parse content plan after {MaxRetries} retries.", maxRetries);
                return;
            }
            
            var contentPlan = new ContentPlan
            {
                WeekStartDate = startOfWeekUtc,
                WeekEndDate = endOfWeekUtc,
                IsConfirmed = false,
                InitialPrompt = settings.SystemPrompt,
                RawAiResponse = aiResponse
            };

            _dbContext.ContentPlans.Add(contentPlan);
            
            foreach (var p in parsedPosts)
            {
                var scheduledTimeUtc = p.ScheduledTime.ToUniversalTime();
                var scheduledTimeNormalized = new DateTimeOffset(scheduledTimeUtc.DateTime, TimeSpan.Zero);

                var newPost = new Post
                {
                    ContentPlanId = contentPlan.Id,
                    Text = p.Text,
                    ScheduledTime = scheduledTimeNormalized,
                    MediaRecommendation = p.MediaRecommendation,
                    Status = PostStatus.WaitingForConfirmation
                };

                contentPlan.Posts.Add(newPost);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var post in contentPlan.Posts)
            {
                BackgroundJob.Enqueue<ImageGenerationJob>(x => x.GenerateImageForPostAsync(post.Id, CancellationToken.None));
            }

            _logger.LogInformation("Successfully generated content plan for current week.");
            
            if (_config.AdminIds != null)
            {
                foreach (var adminId in _config.AdminIds)
                {
                    try
                    {
                        await _botClient.SendTextMessageAsync(adminId, "✅ Контент план на текущую неделю сгенерирован!\nИспользуйте меню, чтобы посмотреть его.", cancellationToken: cancellationToken);
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
            _logger.LogError(ex, "Failed to generate content plan.");
        }
    }

    public async Task GeneratePlanForNextWeekAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting content plan generation for next week...");
        
        var settings = await _dbContext.BotSettings.FirstOrDefaultAsync(cancellationToken);
        if (settings == null || string.IsNullOrEmpty(settings.SystemPrompt))
        {
            _logger.LogWarning("System prompt is not configured. Cannot generate plan.");
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var nextWeekStart = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).AddDays(7).Date;
        var nextWeekEnd = nextWeekStart.AddDays(7).AddTicks(-1);
        
        var nextWeekStartUtc = new DateTimeOffset(nextWeekStart, TimeSpan.Zero);
        var nextWeekEndUtc = new DateTimeOffset(nextWeekEnd, TimeSpan.Zero);

        var existingPlan = await _dbContext.ContentPlans
            .FirstOrDefaultAsync(p => p.WeekStartDate == nextWeekStartUtc, cancellationToken);
            
        if (existingPlan != null)
        {
            _logger.LogInformation("Plan for next week already exists.");
            return;
        }

        var previousPlans = await _dbContext.ContentPlans
            .OrderByDescending(p => p.WeekStartDate)
            .Take(4)
            .Select(p => p.RawAiResponse)
            .ToListAsync(cancellationToken);

        var previousPlansText = string.Join("\n---\n", previousPlans.Where(p => p != null));
        var stats = await GetRecentStatisticsAsync(cancellationToken);

        try
        {
            string aiResponse = string.Empty;
            List<PostDto> parsedPosts = new List<PostDto>();
            int retryCount = 0;
            const int maxRetries = 3;
            bool success = false;

            while (retryCount < maxRetries && !success)
            {
                aiResponse = await _aiService.GenerateContentPlanAsync(settings.SystemPrompt, previousPlansText, stats, nextWeekStartUtc, cancellationToken);
                
                try
                {
                    parsedPosts = ParseAiResponse(aiResponse);
                    if (parsedPosts.Any())
                    {
                        success = true;
                    }
                    else
                    {
                        _logger.LogWarning("Parsed posts list is empty. Retrying... ({RetryCount}/{MaxRetries})", retryCount + 1, maxRetries);
                        retryCount++;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse AI response. Retrying... ({RetryCount}/{MaxRetries})", retryCount + 1, maxRetries);
                    retryCount++;
                }
            }

            if (!success)
            {
                _logger.LogError("Failed to generate and parse content plan after {MaxRetries} retries.", maxRetries);
                return;
            }
            
            var contentPlan = new ContentPlan
            {
                WeekStartDate = nextWeekStartUtc,
                WeekEndDate = nextWeekEndUtc,
                IsConfirmed = false,
                InitialPrompt = settings.SystemPrompt,
                RawAiResponse = aiResponse
            };

            _dbContext.ContentPlans.Add(contentPlan);
            
            foreach (var p in parsedPosts)
            {
                var scheduledTimeUtc = p.ScheduledTime.ToUniversalTime();
                var scheduledTimeNormalized = new DateTimeOffset(scheduledTimeUtc.DateTime, TimeSpan.Zero);

                var newPost = new Post
                {
                    ContentPlanId = contentPlan.Id,
                    Text = p.Text,
                    ScheduledTime = scheduledTimeNormalized,
                    MediaRecommendation = p.MediaRecommendation,
                    Status = PostStatus.WaitingForConfirmation
                };

                contentPlan.Posts.Add(newPost);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            foreach (var post in contentPlan.Posts)
            {
                BackgroundJob.Enqueue<ImageGenerationJob>(x => x.GenerateImageForPostAsync(post.Id, CancellationToken.None));
            }

            // Optional: Notify admins that plan is ready
            _logger.LogInformation("Successfully generated content plan for next week.");
            
            if (_config.AdminIds != null)
            {
                foreach (var adminId in _config.AdminIds)
                {
                    try
                    {
                        await _botClient.SendTextMessageAsync(adminId, "✅ Новый контент план на следующую неделю сгенерирован!\nИспользуйте меню, чтобы посмотреть его.", cancellationToken: cancellationToken);
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
            _logger.LogError(ex, "Failed to generate content plan.");
        }
    }

    private async Task<string> GetRecentStatisticsAsync(CancellationToken cancellationToken)
    {
        var recentStats = await _dbContext.PostStatistics
            .Include(s => s.Post)
            .OrderByDescending(s => s.Post.ScheduledTime)
            .Take(10)
            .ToListAsync(cancellationToken);

        if (!recentStats.Any()) return "No statistics available yet.";

        var statsText = "Recent posts performance:\n";
        foreach (var stat in recentStats)
        {
            statsText += $"- Post snippet: '{stat.Post.Text.Substring(0, Math.Min(30, stat.Post.Text.Length))}...'. Views: {stat.Views}, Reactions: {stat.Reactions}, Reposts: {stat.Reposts}, Comments: {stat.Comments}\n";
        }
        return statsText;
    }

    private List<PostDto> ParseAiResponse(string json)
    {
        // Simple robust JSON extraction assuming the AI returned a JSON array
        var startIdx = json.IndexOf('[');
        var endIdx = json.LastIndexOf(']');
        if (startIdx >= 0 && endIdx >= 0)
        {
            var cleanJson = json.Substring(startIdx, endIdx - startIdx + 1);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<PostDto>>(cleanJson, options) ?? new List<PostDto>();
        }
        
        throw new InvalidOperationException("Could not find a valid JSON array in the AI response.");
    }

    private class PostDto
    {
        public string Text { get; set; } = string.Empty;
        public DateTimeOffset ScheduledTime { get; set; }
        public string? MediaRecommendation { get; set; }
    }
}
