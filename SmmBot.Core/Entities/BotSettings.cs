using SmmBot.Core.Entities;

namespace SmmBot.Core.Entities;

public class BotSettings : BaseEntity
{
    public string? SystemPrompt { get; set; }
    public string? TargetChannelId { get; set; }
    public string? TargetMaxChannelId { get; set; }
    public string? TextModel { get; set; }
    public string? ImageModel { get; set; }
    public string? VideoModel { get; set; }
}
