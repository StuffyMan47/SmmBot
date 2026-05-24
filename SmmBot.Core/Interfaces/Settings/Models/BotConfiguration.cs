namespace SmmBot.Core.Interfaces.Settings.Models;

public class BotConfiguration
{
    public required string Token { get; init; }
    public required string WebhookUrl { get; init; }
    public required string AiToken { get; init; }
    public required string SiteUrl { get; init; }
    public required string SiteToken { get; init; }
    public required string AiUri { get; init; }
    public required GoogleAuth GoogleAuth { get; init; }
    public List<long> AdminIds { get; init; } = [];
}

public class GoogleAuth
{
    public required string Key { get; init; }
}