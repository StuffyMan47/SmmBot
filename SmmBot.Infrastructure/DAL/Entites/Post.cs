using SmmBot.Core.Entities;
using SmmBot.Core.Enums;

namespace SmmBot.Infrastructure.DAL.Entites;

public class Post : BaseEntity
{
    public required long ContentPlanId { get; set; }
    public required string Text { get; set; }
    public string? MediaRecommendation { get; set; }
    public DateTimeOffset ScheduledTime { get; set; }
    public PostStatus Status { get; set; } = PostStatus.WaitingForConfirmation;
    
    // Auto-publication info
    public string? TelegramMessageId { get; set; }

    public ContentPlan ContentPlan { get; set; } = null!;
    public List<MediaFile> MediaFiles { get; set; } = [];
    public PostStatistics? Statistics { get; set; }
}
