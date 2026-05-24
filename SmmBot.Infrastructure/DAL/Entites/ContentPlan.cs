using SmmBot.Core.Entities;

namespace SmmBot.Infrastructure.DAL.Entites;

public class ContentPlan : BaseEntity
{
    public DateTimeOffset WeekStartDate { get; set; }
    public DateTimeOffset WeekEndDate { get; set; }
    public bool IsConfirmed { get; set; }
    public required string InitialPrompt { get; set; }
    public string? RawAiResponse { get; set; }

    public List<Post> Posts { get; set; } = [];
}
