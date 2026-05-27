using Microsoft.EntityFrameworkCore;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using Telegram.Bot.Types;
using SmmBot.Bot.States;
using SmmBot.Bot.BackgroundJobs;
using Hangfire;
using SmmBot.Infrastructure.DAL.Entites;
using SmmBot.Core.Enums;

namespace SmmBot.Bot.Handlers;

public class NextWeekCallbackHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly AppDbContext _dbContext;
    private readonly UserStateCache _stateCache;
    private readonly IBackgroundJobClient _backgroundJobs;

    public NextWeekCallbackHandler(ITelegramBotClient botClient, AppDbContext dbContext, UserStateCache stateCache, IBackgroundJobClient backgroundJobs)
    {
        _botClient = botClient;
        _dbContext = dbContext;
        _stateCache = stateCache;
        _backgroundJobs = backgroundJobs;
    }

    public async Task HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var data = callbackQuery.Data!;

        if (data == "regenerate_next_week_plan")
        {
            _stateCache.SetState(chatId, BotState.WaitingForContentPlanChanges);
            await _botClient.SendTextMessageAsync(chatId, "Напишите, что нужно исправить в контент плане?", cancellationToken: cancellationToken);
        }
        else if (data == "confirm_next_week_plan")
        {
            var now = DateTimeOffset.UtcNow;
            var startOfNextWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).AddDays(7).Date;
            var startOfNextWeekUtc = new DateTimeOffset(startOfNextWeek, TimeSpan.Zero);
            
            var nextPlan = await _dbContext.ContentPlans.Include(p => p.Posts).FirstOrDefaultAsync(p => p.WeekStartDate == startOfNextWeekUtc, cancellationToken);
            if (nextPlan != null)
            {
                nextPlan.IsConfirmed = true;
                foreach (var post in nextPlan.Posts)
                {
                    post.Status = PostStatus.Confirmed;
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _botClient.SendTextMessageAsync(chatId, "✅ Контент план на следующую неделю подтвержден, все посты готовы к публикации.", cancellationToken: cancellationToken);
            }
        }
        else if (data == "view_all_next_week_posts")
        {
            var now = DateTimeOffset.UtcNow;
            var startOfWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).AddDays(7).Date;
            var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);
            
            var startOfWeekUtc = new DateTimeOffset(startOfWeek, TimeSpan.Zero);
            var endOfWeekUtc = new DateTimeOffset(endOfWeek, TimeSpan.Zero);

            var nextPlan = await _dbContext.ContentPlans
                .Include(p => p.Posts)
                .ThenInclude(p => p.MediaFiles)
                .FirstOrDefaultAsync(p => p.WeekStartDate >= startOfWeekUtc && p.WeekEndDate <= endOfWeekUtc, cancellationToken);

            if (nextPlan != null && nextPlan.Posts.Any())
            {
                foreach (var post in nextPlan.Posts.OrderBy(p => p.ScheduledTime))
                {
                    var text = $"📅 {post.ScheduledTime:dd.MM.yyyy HH:mm}\n\n{post.Text}";

                    if (post.MediaFiles.Any())
                    {
                        var mediaGroup = new List<IAlbumInputMedia>();
                        foreach (var media in post.MediaFiles)
                        {
                            if (media.Type == SmmBot.Core.Enums.MediaType.Photo)
                            {
                                if (!string.IsNullOrEmpty(media.FileId))
                                {
                                    mediaGroup.Add(new InputMediaPhoto(InputFile.FromFileId(media.FileId)));
                                }
                                else if (!string.IsNullOrEmpty(media.FilePath))
                                {
                                    mediaGroup.Add(new InputMediaPhoto(SmmBot.Bot.Extensions.MediaHelper.GetInputFile(media.FilePath)));
                                }
                            }
                            else if (media.Type == SmmBot.Core.Enums.MediaType.Video)
                            {
                                if (!string.IsNullOrEmpty(media.FileId))
                                {
                                    mediaGroup.Add(new InputMediaVideo(InputFile.FromFileId(media.FileId)));
                                }
                                else if (!string.IsNullOrEmpty(media.FilePath))
                                {
                                    mediaGroup.Add(new InputMediaVideo(SmmBot.Bot.Extensions.MediaHelper.GetInputFile(media.FilePath, "video.mp4")));
                                }
                            }
                        }

                        if (mediaGroup.Any())
                        {
                            bool sendTextSeparately = text.Length > 1024;
                            
                            if (!sendTextSeparately)
                            {
                                if (mediaGroup.First() is InputMediaPhoto photo)
                                {
                                    photo.Caption = text;
                                }
                                else if (mediaGroup.First() is InputMediaVideo video)
                                {
                                    video.Caption = text;
                                }
                            }
                            
                            await _botClient.SendMediaGroupAsync(chatId, mediaGroup, cancellationToken: cancellationToken);
                            
                            if (sendTextSeparately)
                            {
                                await _botClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken);
                            }
                        }
                        else
                        {
                            await _botClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken);
                        }
                    }
                    else
                    {
                        await _botClient.SendTextMessageAsync(chatId, text, cancellationToken: cancellationToken);
                    }
                }
            }
        }
        else if (data == "generate_next_week_plan")
        {
            await _botClient.SendTextMessageAsync(chatId, "Запущена генерация плана...", cancellationToken: cancellationToken);
            _backgroundJobs.Enqueue<ContentPlanGenerationJob>(x => x.GeneratePlanForNextWeekAsync(CancellationToken.None));
        }

        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }
}
