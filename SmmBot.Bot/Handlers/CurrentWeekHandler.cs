using Microsoft.EntityFrameworkCore;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using SmmBot.Core.Enums;
using SmmBot.Bot.States;
using SmmBot.Infrastructure.DAL.Entites;

namespace SmmBot.Bot.Handlers;

public class CurrentWeekHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly AppDbContext _dbContext;
    private readonly UserStateCache _stateCache;

    public CurrentWeekHandler(ITelegramBotClient botClient, AppDbContext dbContext, UserStateCache stateCache)
    {
        _botClient = botClient;
        _dbContext = dbContext;
        _stateCache = stateCache;
    }

    private async Task ShowContentPlanAsync(Telegram.Bot.Types.Message message, ContentPlan plan, CancellationToken cancellationToken)
    {
        var text = $"📅 Контент план ({plan.WeekStartDate:dd.MM} - {plan.WeekEndDate:dd.MM})\nСтатус: {(plan.IsConfirmed ? "✅ Подтвержден" : "⏳ Не подтвержден")}\n\nПосты:\n";
        
        var inlineKeyboard = new List<InlineKeyboardButton[]>();

        foreach (var post in plan.Posts.OrderBy(p => p.ScheduledTime))
        {
            var statusIcon = post.Status switch
            {
                PostStatus.Confirmed => "✅",
                PostStatus.MissingMedia => "🖼️ (Нет медиа)",
                PostStatus.WaitingForConfirmation => "⏳",
                PostStatus.Published => "📤",
                PostStatus.Cancelled => "❌",
                _ => "📝"
            };

            var postPreview = post.Text.Length > 30 ? post.Text.Substring(0, 30) + "..." : post.Text;
            text += $"\n{statusIcon} {post.ScheduledTime:dd.MM HH:mm} - {postPreview}";

            inlineKeyboard.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"Ред. {post.ScheduledTime:dd.MM HH:mm}", $"edit_post_{post.Id}")
            });
        }

        inlineKeyboard.Add(new[] { InlineKeyboardButton.WithCallbackData("Показать все посты целиком", "view_all_current_week_posts") });
        inlineKeyboard.Add(new[] { InlineKeyboardButton.WithCallbackData("🔄 Перегенерировать план", "regenerate_current_week_plan") });
        if (!plan.IsConfirmed)
        {
            inlineKeyboard.Add(new[] { InlineKeyboardButton.WithCallbackData("✅ Подтвердить контент план", "confirm_current_week_plan") });
        }

        await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: text,
            replyMarkup: new InlineKeyboardMarkup(inlineKeyboard),
            cancellationToken: cancellationToken
        );
    }

    public async Task HandleMenuAsync(Telegram.Bot.Types.Message message, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var startOfWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).Date;
        var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);
        
        var startOfWeekUtc = new DateTimeOffset(startOfWeek, TimeSpan.Zero);
        var endOfWeekUtc = new DateTimeOffset(endOfWeek, TimeSpan.Zero);

        var currentPlan = await _dbContext.ContentPlans
            .Include(p => p.Posts)
            .FirstOrDefaultAsync(p => p.WeekStartDate >= startOfWeekUtc && p.WeekEndDate <= endOfWeekUtc, cancellationToken);

        if (currentPlan == null)
        {
            var inlineKeyboardEmpty = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Сгенерировать план вручную", "generate_current_week_plan") }
            });

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id, 
                text: "Контент план на текущую неделю не найден.", 
                replyMarkup: inlineKeyboardEmpty,
                cancellationToken: cancellationToken);
            return;
        }

        await ShowContentPlanAsync(message, currentPlan, cancellationToken);
    }

    public async Task HandleCallbackAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var data = callbackQuery.Data!;

        if (data.StartsWith("edit_post_"))
        {
            if (long.TryParse(data.Replace("edit_post_", ""), out var postId))
            {
                await ShowPostEditMenuAsync(chatId, postId, cancellationToken);
            }
        }
        else if (data.StartsWith("post_action_"))
        {
            var parts = data.Split('_');
            var action = parts[2];
            if (long.TryParse(parts[3], out var postId))
            {
                await HandlePostActionAsync(chatId, action, postId, cancellationToken);
            }
        }
        else if (data == "view_all_current_week_posts")
        {
            var now = DateTimeOffset.UtcNow;
            var startOfWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).Date;
            var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);

            var startOfWeekUtc = new DateTimeOffset(startOfWeek, TimeSpan.Zero);
            var endOfWeekUtc = new DateTimeOffset(endOfWeek, TimeSpan.Zero);

            var currentPlan = await _dbContext.ContentPlans
                .Include(p => p.Posts)
                .ThenInclude(p => p.MediaFiles)
                .FirstOrDefaultAsync(p => p.WeekStartDate >= startOfWeekUtc && p.WeekEndDate <= endOfWeekUtc, cancellationToken);

            if (currentPlan != null && currentPlan.Posts.Any())
            {
                foreach (var post in currentPlan.Posts.OrderBy(p => p.ScheduledTime))
                {
                    var text = $"📅 {post.ScheduledTime:dd.MM.yyyy HH:mm}\n\n{post.Text}";

                    if (post.MediaFiles.Any())
                    {
                        var mediaGroup = new List<IAlbumInputMedia>();
                        foreach (var media in post.MediaFiles)
                        {
                            if (media.Type == MediaType.Photo)
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
                            else if (media.Type == MediaType.Video)
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
        else if (data.StartsWith("back_to_plan_"))
        {
            if (long.TryParse(data.Replace("back_to_plan_", ""), out var postId))
            {
                var post = await _dbContext.Posts
                    .Include(p => p.ContentPlan)
                    .ThenInclude(x=>x.Posts)
                    .FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
                    
                if (post?.ContentPlan != null)
                {
                    await ShowContentPlanAsync(callbackQuery.Message!, post.ContentPlan, cancellationToken);
                }
                else
                {
                    // Fallback to finding current week's plan if post is deleted
                    var now = DateTimeOffset.UtcNow;
                    var startOfWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).Date;
                    var endOfWeek = startOfWeek.AddDays(7).AddTicks(-1);
                    
                    var startOfWeekUtc = new DateTimeOffset(startOfWeek, TimeSpan.Zero);
                    var endOfWeekUtc = new DateTimeOffset(endOfWeek, TimeSpan.Zero);
                    
                    var currentPlan = await _dbContext.ContentPlans
                        .Include(p => p.Posts)
                        .FirstOrDefaultAsync(p => p.WeekStartDate >= startOfWeekUtc && p.WeekEndDate <= endOfWeekUtc, cancellationToken);

                    if (currentPlan != null)
                    {
                        await ShowContentPlanAsync(callbackQuery.Message!, currentPlan, cancellationToken);
                    }
                }
            }
        }

        else if (data == "generate_current_week_plan")
        {
            await _botClient.SendTextMessageAsync(chatId, "Запущена генерация плана...", cancellationToken: cancellationToken);
            Hangfire.BackgroundJob.Enqueue<SmmBot.Bot.BackgroundJobs.ContentPlanGenerationJob>(x => x.GeneratePlanForCurrentWeekAsync(CancellationToken.None));
        }
        else if (data == "regenerate_current_week_plan")
        {
            _stateCache.SetState(chatId, BotState.WaitingForContentPlanChanges);
            await _botClient.SendTextMessageAsync(chatId, "Напишите, что нужно исправить в контент плане на эту неделю?", cancellationToken: cancellationToken);
        }
        else if (data == "confirm_next_week_plan")
        {
            var now = DateTimeOffset.UtcNow;
            var startOfWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).AddDays(7).Date;
            var startOfWeekUtc = new DateTimeOffset(startOfWeek, TimeSpan.Zero);
            
            var currentPlan = await _dbContext.ContentPlans.Include(p => p.Posts).FirstOrDefaultAsync(p => p.WeekStartDate == startOfWeekUtc, cancellationToken);
            if (currentPlan != null)
            {
                currentPlan.IsConfirmed = true;
                foreach (var post in currentPlan.Posts)
                {
                    post.Status = PostStatus.Confirmed;
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _botClient.SendTextMessageAsync(chatId, "✅ Контент план на следующую неделю подтвержден, все посты готовы к публикации.", cancellationToken: cancellationToken);
            }
        }
        else if (data == "confirm_current_week_plan")
        {
            var now = DateTimeOffset.UtcNow;
            var startOfWeek = now.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).Date;
            var startOfWeekUtc = new DateTimeOffset(startOfWeek, TimeSpan.Zero);
            
            var currentPlan = await _dbContext.ContentPlans.Include(p => p.Posts).FirstOrDefaultAsync(p => p.WeekStartDate == startOfWeekUtc, cancellationToken);
            if (currentPlan != null)
            {
                currentPlan.IsConfirmed = true;
                foreach (var post in currentPlan.Posts)
                {
                    post.Status = PostStatus.Confirmed;
                }
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _botClient.SendTextMessageAsync(chatId, "✅ Контент план на текущую неделю подтвержден, все посты готовы к публикации.", cancellationToken: cancellationToken);
            }
        }

        await _botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }

    private async Task ShowPostEditMenuAsync(long chatId, long postId, CancellationToken cancellationToken)
    {
        var post = await _dbContext.Posts
            .Include(p => p.MediaFiles)
            .FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);

        if (post == null) return;

        var mediaRec = string.IsNullOrEmpty(post.MediaRecommendation) ? "Отсутствует" : post.MediaRecommendation;
        var text = $"Редактирование поста\nДата: {post.ScheduledTime:dd.MM.yyyy HH:mm}\nСтатус: {post.Status}\n\n📸 Медиа-рекомендация от ИИ:\n{mediaRec}\n\nТекст:\n{post.Text}";

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("Изменить дату и время", $"post_action_time_{postId}") },
            new[] { InlineKeyboardButton.WithCallbackData("Изменить текст", $"post_action_text_{postId}") },
            new[] { InlineKeyboardButton.WithCallbackData("Сгенерировать медиа (ИИ)", $"post_action_genmedia_{postId}") },
            new[] { InlineKeyboardButton.WithCallbackData("Прикрепить медиа", $"post_action_attach_{postId}") },
            new[] 
            { 
                InlineKeyboardButton.WithCallbackData("✅ Подтвердить", $"post_action_confirm_{postId}"),
                InlineKeyboardButton.WithCallbackData("❌ Удалить", $"post_action_delete_{postId}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад к плану", $"back_to_plan_{postId}") }
        });

        if (post.MediaFiles.Any())
        {
            var mediaGroup = new List<IAlbumInputMedia>();
            foreach (var media in post.MediaFiles)
            {
                if (media.Type == MediaType.Photo)
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
                else if (media.Type == MediaType.Video)
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
                // Telegram API restricts media caption length to 1024 characters.
                // If text is too long, send media without caption and then send text separately.
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
                    await _botClient.SendTextMessageAsync(chatId, text, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
                }
                else
                {
                    await _botClient.SendTextMessageAsync(chatId, "Выберите действие:", replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
                }
            }
            else
            {
                await _botClient.SendTextMessageAsync(chatId, text, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
            }
        }
        else
        {
            await _botClient.SendTextMessageAsync(chatId, text, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
        }
    }

    private async Task HandlePostActionAsync(long chatId, string action, long postId, CancellationToken cancellationToken)
    {
        var post = await _dbContext.Posts.FirstOrDefaultAsync(p => p.Id == postId, cancellationToken);
        if (post == null) return;

        switch (action)
        {
            case "time":
                _stateCache.SetState(chatId, BotState.WaitingForPostTimeEdit, postId);
                await _botClient.SendTextMessageAsync(chatId, "Отправьте новую дату и время в формате ДД.ММ.ГГГГ ЧЧ:ММ", cancellationToken: cancellationToken);
                break;
            case "text":
                _stateCache.SetState(chatId, BotState.WaitingForPostTextEdit, postId);
                await _botClient.SendTextMessageAsync(chatId, "Отправьте новый текст поста.", cancellationToken: cancellationToken);
                break;
            case "genmedia":
                await _botClient.SendTextMessageAsync(chatId, "Генерация медиа запущена...", cancellationToken: cancellationToken);
                Hangfire.BackgroundJob.Enqueue<SmmBot.Bot.BackgroundJobs.ImageGenerationJob>(x => x.GenerateImageForPostAsync(postId, CancellationToken.None));
                break;
            case "attach":
                _stateCache.SetState(chatId, BotState.WaitingForPostMedia, postId);
                await _botClient.SendTextMessageAsync(chatId, "Отправьте фото (можно несколько сразу) для прикрепления к посту. По завершении отправьте текст 'Готово'.", cancellationToken: cancellationToken);
                break;
            case "confirm":
                post.Status = PostStatus.Confirmed;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await _botClient.SendTextMessageAsync(chatId, "✅ Пост подтвержден и будет опубликован в назначенное время.", cancellationToken: cancellationToken);
                break;
            case "delete":
                var confirmDeleteKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new[]
                    {
                        InlineKeyboardButton.WithCallbackData("🗑 Да, удалить", $"post_action_confirmdelete_{postId}"),
                        InlineKeyboardButton.WithCallbackData("Отменить", $"edit_post_{postId}")
                    }
                });
                await _botClient.SendTextMessageAsync(chatId, "Вы уверены, что хотите удалить этот пост?", replyMarkup: confirmDeleteKeyboard, cancellationToken: cancellationToken);
                break;
            case "confirmdelete":
                _dbContext.Posts.Remove(post);
                await _dbContext.SaveChangesAsync(cancellationToken);
                
                var backToPlanKeyboard = new InlineKeyboardMarkup(new[]
                {
                    // Sending a dummy postId for deleted post to fallback to current week plan
                    new[] { InlineKeyboardButton.WithCallbackData("🔙 Назад к плану", $"back_to_plan_{post.Id}") } 
                });
                await _botClient.SendTextMessageAsync(chatId, "🗑 Пост удален.", replyMarkup: backToPlanKeyboard, cancellationToken: cancellationToken);
                break;
        }
    }
}
