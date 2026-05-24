using Microsoft.EntityFrameworkCore;
using SmmBot.Infrastructure.DAL.DbContext;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using SmmBot.Bot.States;
using SmmBot.Core.Interfaces.Ai;

namespace SmmBot.Bot.Handlers;

public class NextWeekHandler
{
    private readonly ITelegramBotClient _botClient;
    private readonly AppDbContext _dbContext;
    private readonly UserStateCache _stateCache;
    private readonly IAiService _aiService;

    public NextWeekHandler(ITelegramBotClient botClient, AppDbContext dbContext, UserStateCache stateCache, IAiService aiService)
    {
        _botClient = botClient;
        _dbContext = dbContext;
        _stateCache = stateCache;
        _aiService = aiService;
    }

    public async Task HandleMenuAsync(Telegram.Bot.Types.Message message, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
    
        // 1. Рассчитываем даты как DateTime (обрезаем время до 00:00)
        var startOfWeekDate = now.Date.AddDays(-(int)now.DayOfWeek + (int)DayOfWeek.Monday).AddDays(7);
        var endOfWeekDate = startOfWeekDate.AddDays(7).AddTicks(-1);

        // 2. Явно создаем DateTimeOffset с нулевым смещением (UTC) для корректной работы с timestamptz
        var startOfWeekUtc = new DateTimeOffset(startOfWeekDate, TimeSpan.Zero);
        var endOfWeekUtc = new DateTimeOffset(endOfWeekDate, TimeSpan.Zero);

        var nextPlan = await _dbContext.ContentPlans
            .Include(p => p.Posts)
            .FirstOrDefaultAsync(p => p.WeekStartDate >= startOfWeekUtc && p.WeekEndDate <= endOfWeekUtc, cancellationToken);

        if (nextPlan == null)
        {
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("Сгенерировать план вручную", "generate_next_week_plan") }
            });

            await _botClient.SendTextMessageAsync(
                chatId: message.Chat.Id, 
                text: "Контент план на следующую неделю еще не создан.", 
                replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
            return;
        }

        var text = $"📅 Контент план на следующую неделю ({startOfWeekUtc:dd.MM} - {endOfWeekUtc:dd.MM})\nСтатус: {(nextPlan.IsConfirmed ? "✅ Подтвержден" : "⏳ Не подтвержден")}\n\nПосты:\n";
        
        var inlineKeyboardPlan = new List<InlineKeyboardButton[]>();

        foreach (var post in nextPlan.Posts.OrderBy(p => p.ScheduledTime))
        {
            var statusIcon = post.Status switch
            {
                SmmBot.Core.Enums.PostStatus.Confirmed => "✅",
                SmmBot.Core.Enums.PostStatus.WaitingForConfirmation => "⏳",
                _ => "📝"
            };

            var postPreview = post.Text.Length > 30 ? post.Text.Substring(0, 30) + "..." : post.Text;
            text += $"\n{statusIcon} {post.ScheduledTime:dd.MM HH:mm} - {postPreview}";

            inlineKeyboardPlan.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData($"Ред. {post.ScheduledTime:dd.MM HH:mm}", $"edit_post_{post.Id}")
            });
        }

        inlineKeyboardPlan.Add(new[] { InlineKeyboardButton.WithCallbackData("Показать все посты целиком", "view_all_next_week_posts") });
        inlineKeyboardPlan.Add(new[] { InlineKeyboardButton.WithCallbackData("🔄 Перегенерировать план", "regenerate_next_week_plan") });
        if (!nextPlan.IsConfirmed)
        {
            inlineKeyboardPlan.Add(new[] { InlineKeyboardButton.WithCallbackData("✅ Подтвердить контент план", "confirm_next_week_plan") });
        }

        await _botClient.SendTextMessageAsync(
            chatId: message.Chat.Id,
            text: text,
            replyMarkup: new InlineKeyboardMarkup(inlineKeyboardPlan),
            cancellationToken: cancellationToken
        );
    }
}
