using SmmBot.Core.Entities;
using SmmBot.Core.Enums;

namespace SmmBot.Infrastructure.DAL.Entites;

public class MediaFile : BaseEntity
{
    public required long PostId { get; set; }
    public MediaType Type { get; set; }
    public required string FilePath { get; set; } // Path in S3 or local storage
    public string? FileId { get; set; } // Telegram FileId for reuse

    public Post Post { get; set; } = null!;
}
