namespace SmmBot.Bot.States;

public enum BotState
{
    None = 0,
    WaitingForSystemPrompt = 1,
    WaitingForTargetChannel = 2,
    WaitingForContentPlanChanges = 3,
    WaitingForPostTextEdit = 4,
    WaitingForPostTimeEdit = 5,
    WaitingForPostMedia = 6
}
