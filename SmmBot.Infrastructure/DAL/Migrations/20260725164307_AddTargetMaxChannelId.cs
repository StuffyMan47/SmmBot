using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmmBot.Infrastructure.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTargetMaxChannelId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TargetMaxChannelId",
                table: "BotSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetMaxChannelId",
                table: "BotSettings");
        }
    }
}
