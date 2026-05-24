using SmmBot.Core.Extensions;
using Microsoft.EntityFrameworkCore;
using SmmBot.Infrastructure.DAL.Entites;
using SmmBot.Core.Entities;

namespace SmmBot.Infrastructure.DAL.DbContext;

public partial class AppDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        
        Configure(modelBuilder.Entity<User>());
        Configure(modelBuilder.Entity<Message>());
        Configure(modelBuilder.Entity<Chat>());
        Configure(modelBuilder.Entity<ContentPlan>());
        Configure(modelBuilder.Entity<Post>());
        Configure(modelBuilder.Entity<MediaFile>());
        Configure(modelBuilder.Entity<PostStatistics>());
        Configure(modelBuilder.Entity<BotSettings>());
    }
    
    protected static void ApplySnakeCaseNames(ModelBuilder modelBuilder)
    {
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(entity.GetDefaultTableName()!.ToLowerCaseWithUnderscore());

            foreach (var property in entity.GetProperties())
            {
                property.SetColumnName(property.GetColumnName().ToLowerCaseWithUnderscore());
            }
        }
    }
    
    protected static void DisableCascadeDelete(ModelBuilder modelBuilder)
    {
        var cascadeFKs = modelBuilder.Model.GetEntityTypes()
            .SelectMany(t => t.GetForeignKeys())
            .Where(fk => fk is { IsOwnership: false, DeleteBehavior: DeleteBehavior.Cascade });

        foreach (var fk in cascadeFKs)
            fk.DeleteBehavior = DeleteBehavior.Restrict;
    }
}