using SmmBot.Core.Entities;

namespace SmmBot.Core.Entities;

public class AiTokensLog : BaseEntity
{
    public required string OperationType { get; set; }
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
    public DateTimeOffset Date { get; set; } = DateTimeOffset.UtcNow;
}
