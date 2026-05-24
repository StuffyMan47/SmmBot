using SmmBot.Core.Entities;

namespace SmmBot.Infrastructure.DAL.Entites;

public class PostStatistics : BaseEntity
{
    public required long PostId { get; set; }
    public int Views { get; set; }
    public int Reactions { get; set; }
    public int Reposts { get; set; }
    public int Comments { get; set; }
    public DateTimeOffset LastUpdated { get; set; }

    public Post Post { get; set; } = null!;
}
