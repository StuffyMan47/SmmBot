using SmmBot.Infrastructure.DAL.Entites;
using SmmBot.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SmmBot.Infrastructure.DAL.DbContext;

public partial class AppDbContext
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<Chat> Chats => Set<Chat>();
    public DbSet<ContentPlan> ContentPlans => Set<ContentPlan>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<MediaFile> MediaFiles => Set<MediaFile>();
    public DbSet<PostStatistics> PostStatistics => Set<PostStatistics>();
    public DbSet<BotSettings> BotSettings => Set<BotSettings>();
    
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.TelegramId).IsUnique();
    }

    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasOne(e => e.User)
            .WithMany(u => u.Messages)
            .HasForeignKey(e => e.UserId);
        
        builder.HasOne(e => e.Chat)
            .WithMany(c => c.Messages)
            .HasForeignKey(e => e.ChatId);
    }
    
    public void Configure(EntityTypeBuilder<Chat> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasMany(x => x.Users).WithMany(x => x.Chats);
    }

    public void Configure(EntityTypeBuilder<ContentPlan> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasMany(x => x.Posts)
               .WithOne(p => p.ContentPlan)
               .HasForeignKey(p => p.ContentPlanId);
    }

    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasMany(x => x.MediaFiles)
               .WithOne(m => m.Post)
               .HasForeignKey(m => m.PostId);
        builder.HasOne(x => x.Statistics)
               .WithOne(s => s.Post)
               .HasForeignKey<PostStatistics>(s => s.PostId);
    }

    public void Configure(EntityTypeBuilder<MediaFile> builder)
    {
        builder.HasKey(x => x.Id);
    }

    public void Configure(EntityTypeBuilder<PostStatistics> builder)
    {
        builder.HasKey(x => x.Id);
    }

    public void Configure(EntityTypeBuilder<BotSettings> builder)
    {
        builder.HasKey(x => x.Id);
    }
}