namespace SmmBot.Core.Interfaces.Ai;

public interface IAiService
{
    Task<string> GenerateContentPlanAsync(string systemPrompt, string previousPlans, string statistics, DateTimeOffset targetWeekStart, CancellationToken cancellationToken = default);
    Task<string> EditContentPlanAsync(string currentPlan, string userPrompt, CancellationToken cancellationToken = default);
    Task<string> GenerateImagePromptAsync(string postText, CancellationToken cancellationToken = default);
    Task<string?> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default);
    Task<string?> GenerateVideoAsync(string prompt, CancellationToken cancellationToken = default);
}
