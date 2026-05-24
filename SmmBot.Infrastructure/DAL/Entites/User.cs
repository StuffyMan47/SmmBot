using SmmBot.Core.Entities;
using SmmBot.Core.Enums;

namespace SmmBot.Infrastructure.DAL.Entites;

public class User : BaseEntity
{
    public long TelegramId { get; init; }
    public required string Username { get; init; }
    public DateTime LastActivity { get; init; } = DateTime.UtcNow;
    public UserState State { get; init; } = UserState.Active;
    public UserType Type { get; init; } = UserType.Subscriber;
    
    // Навигационные свойства
    public List<Message> Messages { get; init; } = [];
    public List<Chat> Chats { get; init; } = [];
}