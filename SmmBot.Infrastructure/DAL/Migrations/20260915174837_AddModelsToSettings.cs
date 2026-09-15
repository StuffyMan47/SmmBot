using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmmBot.Infrastructure.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddModelsToSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageModel",
                table: "BotSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TextModel",
                table: "BotSettings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VideoModel",
                table: "BotSettings",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageModel",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "TextModel",
                table: "BotSettings");

            migrationBuilder.DropColumn(
                name: "VideoModel",
                table: "BotSettings");
        }
    }
}
