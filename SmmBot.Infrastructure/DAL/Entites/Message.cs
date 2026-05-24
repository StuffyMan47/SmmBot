using SmmBot.Core.Entities;

namespace SmmBot.Infrastructure.DAL.Entites;

public class Message : BaseEntity
{
    public required string Content { get; init; }
    public required long UserId { get; init; }
    public required long ChatId { get; init; } 
    
    public User User { get; init; }
    public Chat Chat { get; init; }
}