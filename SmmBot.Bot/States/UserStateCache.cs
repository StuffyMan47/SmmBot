using System.Collections.Concurrent;

namespace SmmBot.Bot.States;

public class UserStateCache
{
    private readonly ConcurrentDictionary<long, UserStateData> _states = new();

    public UserStateData GetState(long chatId)
    {
        return _states.GetOrAdd(chatId, new UserStateData { State = BotState.None });
    }

    public void SetState(long chatId, BotState state, object? data = null)
    {
        var userData = GetState(chatId);
        userData.State = state;
        userData.Data = data;
    }

    public void ClearState(long chatId)
    {
        SetState(chatId, BotState.None);
    }
}

public class UserStateData
{
    public BotState State { get; set; }
    public object? Data { get; set; }
}
