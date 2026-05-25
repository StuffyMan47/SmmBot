using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmmBot.Infrastructure.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaRecommendationToPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "MediaRecommendation",
                table: "Posts",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MediaRecommendation",
                table: "Posts");
        }
    }
}
