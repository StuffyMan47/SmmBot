using SmmBot.Core.Entities;

namespace SmmBot.Infrastructure.DAL.Entites;

public class Chat : BaseEntity
{
    public required string Name { get; set; }
    public required long TelegramId { get; set; } 
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; init; } =  DateTimeOffset.UtcNow;

    public List<Message> Messages { get; set; } = [];
    public List<User> Users { get; set; } = [];
}