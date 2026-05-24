using SmmBot.Core.Entities;

namespace SmmBot.Core.Entities;

public class BotSettings : BaseEntity
{
    public string? SystemPrompt { get; set; }
    public string? TargetChannelId { get; set; }
}
